using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.JelevisionExtras.Matching;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

/// <summary>
/// Reads physical-disc title metadata from TheDiscDb's public GraphQL endpoint.
/// </summary>
public sealed class TheDiscDbClient
{
    private const int BatchSize = 50;

    private const string ExtrasQuery = """
        query JelevisionExtras($tmdbIds: [String!]!) {
          mediaItems(
            first: 100
            where: { externalids: { tmdb: { in: $tmdbIds } } }
          ) {
            nodes {
              externalids {
                tmdb
              }
              releases {
                slug
                title
                discs {
                  slug
                  name
                  contentHash
                  titles(order: { index: ASC }) {
                    index
                    comment
                    sourceFile
                    duration
                    item {
                      title
                      type
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<TheDiscDbClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TheDiscDbClient"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="logger">Logger.</param>
    public TheDiscDbClient(HttpClient httpClient, ILogger<TheDiscDbClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Loads candidate discs for the requested TMDb movie ids.
    /// </summary>
    /// <param name="tmdbIds">TMDb ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Candidates keyed by TMDb id.</returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DiscCandidate>>> GetCandidatesAsync(
        IReadOnlyCollection<string> tmdbIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = tmdbIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var result = new Dictionary<string, List<DiscCandidate>>(StringComparer.Ordinal);

        foreach (var batch in distinctIds.Chunk(BatchSize))
        {
            var payload = new
            {
                query = ExtrasQuery,
                variables = new
                {
                    tmdbIds = batch
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "graphql",
                payload,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content
                .ReadFromJsonAsync<GraphQlEnvelope<ExtrasCatalogData>>(
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (envelope?.Errors is { Count: > 0 })
            {
                var message = string.Join(
                    "; ",
                    envelope.Errors.Select(error => error.Message ?? "Unknown GraphQL error"));
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"TheDiscDb returned GraphQL errors: {message}"));
            }

            foreach (var mediaItem in envelope?.Data?.MediaItems?.Nodes ?? [])
            {
                AddMediaItemCandidates(result, mediaItem);
            }
        }

        _logger.LogInformation(
            "Loaded {DiscCount} TheDiscDb disc candidates for {MovieCount} movies",
            result.Values.Sum(value => value.Count),
            result.Count);

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<DiscCandidate>)pair.Value,
            StringComparer.Ordinal);
    }

    private static void AddMediaItemCandidates(
        IDictionary<string, List<DiscCandidate>> result,
        DiscDbMediaItem mediaItem)
    {
        var tmdbId = mediaItem.ExternalIds?.Tmdb;
        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            return;
        }

        if (!result.TryGetValue(tmdbId, out var candidates))
        {
            candidates = [];
            result[tmdbId] = candidates;
        }

        foreach (var release in mediaItem.Releases)
        {
            foreach (var disc in release.Discs)
            {
                var extras = disc.Titles
                    .Where(title =>
                        title.Item is not null
                        && !string.IsNullOrWhiteSpace(title.Item.Title)
                        && !string.IsNullOrWhiteSpace(title.Item.Type)
                        && TryParseDuration(title.Duration, out _))
                    .Select(title =>
                    {
                        _ = TryParseDuration(title.Duration, out var seconds);
                        return new DiscExtra(
                            title.Index,
                            title.Comment,
                            title.SourceFile,
                            seconds,
                            title.Item!.Title!,
                            title.Item.Type!);
                    })
                    .Where(extra => ExtraTypeMapper.Map(extra.DiscDbType, extra.Title).HasValue)
                    .ToList();

                if (extras.Count == 0)
                {
                    continue;
                }

                candidates.Add(new DiscCandidate(
                    tmdbId,
                    release.Slug ?? "unknown-release",
                    release.Title ?? "Unknown release",
                    disc.Slug ?? "unknown-disc",
                    disc.Name ?? "Unknown disc",
                    disc.ContentHash,
                    extras));
            }
        }
    }

    internal static bool TryParseDuration(string? value, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration))
        {
            return false;
        }

        seconds = duration.TotalSeconds;
        return seconds > 0;
    }
}
