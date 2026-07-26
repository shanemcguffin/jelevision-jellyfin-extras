using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.JelevisionExtras.Overrides;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

/// <summary>
/// One validated community-catalog download.
/// </summary>
/// <param name="Version">Published catalog version.</param>
/// <param name="EntryCount">Number of verified source entries.</param>
/// <param name="Rules">Provider-specific rules compiled for local matching.</param>
public sealed record CommunityCatalogSnapshot(
    string Version,
    int EntryCount,
    IReadOnlyList<CuratedOverrideRule> Rules);

/// <summary>
/// Downloads a configured Jelevision catalog without sending library identifiers.
/// </summary>
public sealed class CommunityCatalogClient
{
    /// <summary>
    /// Gets the default public-seed catalog endpoint.
    /// </summary>
    public const string DefaultCatalogUrl =
        "https://raw.githubusercontent.com/shanemcguffin/jelevision-jellyfin-extras/main/catalog-format/public-sample.json";

    private const int MaximumEntries = 250_000;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<CommunityCatalogClient> _logger;
    private readonly Uri? _endpointOverride;
    private readonly string? _accessTokenOverride;

    /// <summary>
    /// Initializes a catalog client using plugin configuration.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="logger">Logger.</param>
    public CommunityCatalogClient(
        HttpClient httpClient,
        ILogger<CommunityCatalogClient> logger)
        : this(httpClient, logger, null, null)
    {
    }

    private CommunityCatalogClient(
        HttpClient httpClient,
        ILogger<CommunityCatalogClient> logger,
        Uri? endpointOverride,
        string? accessTokenOverride)
    {
        _httpClient = httpClient;
        _logger = logger;
        _endpointOverride = endpointOverride;
        _accessTokenOverride = accessTokenOverride;
    }

    /// <summary>
    /// Creates an explicitly addressed client for deterministic tests and
    /// direct self-hosted integrations.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="endpoint">Absolute catalog snapshot endpoint.</param>
    /// <param name="accessToken">Optional Bearer token.</param>
    /// <returns>A client pinned to <paramref name="endpoint"/>.</returns>
    public static CommunityCatalogClient CreateForEndpoint(
        HttpClient httpClient,
        ILogger<CommunityCatalogClient> logger,
        Uri endpoint,
        string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return new CommunityCatalogClient(
            httpClient,
            logger,
            endpoint,
            accessToken);
    }

    /// <summary>
    /// Loads and validates the current verified community catalog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validated catalog metadata and matching rules.</returns>
    public async Task<CommunityCatalogSnapshot> GetVerifiedRulesAsync(
        CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint();
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var accessToken = ResolveAccessToken();
        if (accessToken is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var document = await response.Content
            .ReadFromJsonAsync<CommunityCatalogDocument>(
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The Jelevision community catalog returned an empty document.");
        var snapshot = Compile(document);
        _logger.LogInformation(
            "Loaded Jelevision community catalog {CatalogVersion}: {EntryCount} verified entries and {RuleCount} provider rules",
            snapshot.Version,
            snapshot.EntryCount,
            snapshot.Rules.Count);
        return snapshot;
    }

    private static CommunityCatalogSnapshot Compile(
        CommunityCatalogDocument document)
    {
        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Unsupported Jelevision catalog schema {document.SchemaVersion}."));
        }

        var version = RequireText(document.CatalogVersion, "catalogVersion");
        if (document.Items.Count is 0 or > MaximumEntries)
        {
            throw new InvalidOperationException(
                "The Jelevision catalog must contain between 1 and 250000 entries.");
        }

        var sources = document.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Id))
            .GroupBy(source => source.Id!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => FormatSource(group.First()),
                StringComparer.Ordinal);
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var selectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rules = new List<CuratedOverrideRule>();

        foreach (var item in document.Items)
        {
            var entryId = RequireText(item.Id, "item.id");
            if (!entryIds.Add(entryId))
            {
                throw new InvalidOperationException(
                    $"Duplicate Jelevision catalog entry id: {entryId}");
            }

            var match = item.Match
                ?? throw new InvalidOperationException(
                    $"{entryId}: match is required.");
            if (match.TitleOrdinal < 0
                || !double.IsFinite(match.DurationSeconds)
                || match.DurationSeconds <= 0
                || !double.IsFinite(match.ToleranceSeconds)
                || match.ToleranceSeconds is <= 0 or > 5)
            {
                throw new InvalidOperationException(
                    $"{entryId}: invalid ordinal, runtime, or tolerance.");
            }

            var verification = item.Verification
                ?? throw new InvalidOperationException(
                    $"{entryId}: verification is required.");
            if (!string.Equals(
                    verification.Status,
                    "verified",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{entryId}: only verified records may be compiled.");
            }

            var result = item.Result
                ?? throw new InvalidOperationException(
                    $"{entryId}: result is required.");
            var action = ParseAction(entryId, result);
            var title = action == CuratedOverrideAction.SetMetadata
                ? RequireText(result.Title, $"{entryId}.result.title")
                : null;
            ExtraType? extraType = action == CuratedOverrideAction.SetMetadata
                ? ParseExtraType(entryId, result.Type)
                : null;
            var source = BuildSource(verification.SourceIds, sources);
            var reason = verification.Evidence.Count > 0
                ? string.Join(" ", verification.Evidence)
                : "Verified Jelevision community catalog record.";
            var ids = item.Media?.Ids
                ?? throw new InvalidOperationException(
                    $"{entryId}: media ids are required.");
            var providerIds = GetProviderIds(entryId, ids);

            foreach (var (kind, parentId) in providerIds)
            {
                var selector = string.Join(
                    '|',
                    kind,
                    parentId,
                    match.TitleOrdinal.ToString(CultureInfo.InvariantCulture),
                    match.DurationSeconds.ToString("R", CultureInfo.InvariantCulture));
                if (!selectors.Add(selector))
                {
                    throw new InvalidOperationException(
                        $"{entryId}: duplicate verified selector {selector}.");
                }

                rules.Add(new CuratedOverrideRule(
                    $"{entryId}:{kind.ToString().ToLowerInvariant()}",
                    kind,
                    parentId,
                    match.TitleOrdinal,
                    match.DurationSeconds,
                    match.ToleranceSeconds,
                    action,
                    title,
                    extraType,
                    source,
                    reason));
            }
        }

        return new CommunityCatalogSnapshot(version, document.Items.Count, rules);
    }

    private Uri ResolveEndpoint()
    {
        if (_endpointOverride is not null)
        {
            return _endpointOverride;
        }

        var environmentUrl = Environment.GetEnvironmentVariable(
            "JELEVISION_EXTRAS_CATALOG_URL");
        var configuredUrl = !string.IsNullOrWhiteSpace(environmentUrl)
            ? environmentUrl
            : Plugin.Instance?.Configuration.CommunityCatalogUrl;
        configuredUrl = string.IsNullOrWhiteSpace(configuredUrl)
            ? DefaultCatalogUrl
            : configuredUrl;

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps
                && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                "CommunityCatalogUrl must be an absolute HTTP or HTTPS URL.");
        }

        return endpoint;
    }

    private string? ResolveAccessToken()
    {
        if (_endpointOverride is not null)
        {
            return NormalizeSecret(_accessTokenOverride);
        }

        var environmentToken = Environment.GetEnvironmentVariable(
            "JELEVISION_EXTRAS_CATALOG_TOKEN");
        var configuredToken = !string.IsNullOrWhiteSpace(environmentToken)
            ? environmentToken
            : Plugin.Instance?.Configuration.CommunityCatalogAccessToken;
        return NormalizeSecret(configuredToken);
    }

    private static IReadOnlyList<(ParentIdKind Kind, string Id)> GetProviderIds(
        string entryId,
        CommunityCatalogMediaIds ids)
    {
        var result = new List<(ParentIdKind Kind, string Id)>();
        if (!string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            if (!ids.Tmdb.All(char.IsAsciiDigit))
            {
                throw new InvalidOperationException(
                    $"{entryId}: TMDb id must contain only digits.");
            }

            result.Add((ParentIdKind.Tmdb, ids.Tmdb));
        }

        if (!string.IsNullOrWhiteSpace(ids.Imdb))
        {
            if (ids.Imdb.Length > 2
                && ids.Imdb.StartsWith("tt", StringComparison.Ordinal)
                && !ids.Imdb.AsSpan(2).ContainsAnyExceptInRange('0', '9'))
            {
                result.Add((ParentIdKind.Imdb, ids.Imdb));
            }
            else
            {
                throw new InvalidOperationException(
                    $"{entryId}: IMDb id must use the tt1234567 format.");
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                $"{entryId}: at least one TMDb or IMDb id is required.");
        }

        return result;
    }

    private static CuratedOverrideAction ParseAction(
        string entryId,
        CommunityCatalogResult result)
    {
        if (string.Equals(
                result.Action,
                "set_metadata",
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    result.Type,
                    "technical",
                    StringComparison.Ordinal)
                || string.Equals(
                    result.Type,
                    "duplicate",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{entryId}: hidden records cannot set visible metadata.");
            }

            return CuratedOverrideAction.SetMetadata;
        }

        if (string.Equals(
                result.Action,
                "hide_technical",
                StringComparison.Ordinal)
            && string.Equals(
                result.Type,
                "technical",
                StringComparison.Ordinal)
            && result.Title is null)
        {
            return CuratedOverrideAction.HideTechnical;
        }

        if (string.Equals(
                result.Action,
                "hide_duplicate",
                StringComparison.Ordinal)
            && string.Equals(
                result.Type,
                "duplicate",
                StringComparison.Ordinal)
            && result.Title is null)
        {
            return CuratedOverrideAction.HideDuplicate;
        }

        throw new InvalidOperationException(
            $"{entryId}: result action and type are inconsistent.");
    }

    private static ExtraType ParseExtraType(string entryId, string? value)
    {
        return value switch
        {
            "trailer" => ExtraType.Trailer,
            "featurette" => ExtraType.Featurette,
            "deleted_scene" => ExtraType.DeletedScene,
            "behind_the_scenes" => ExtraType.BehindTheScenes,
            "interview" => ExtraType.Interview,
            "scene" => ExtraType.Scene,
            "short" => ExtraType.Short,
            "other" => ExtraType.Clip,
            _ => throw new InvalidOperationException(
                $"{entryId}: unsupported extra type {value}.")
        };
    }

    private static string BuildSource(
        IReadOnlyList<string> sourceIds,
        IReadOnlyDictionary<string, string> sources)
    {
        var values = sourceIds
            .Select(id => sources.TryGetValue(id, out var source) ? source : id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        values.Insert(
            0,
            "https://github.com/shanemcguffin/jelevision-jellyfin-extras/tree/main/catalog-format");
        return string.Join("; ", values);
    }

    private static string FormatSource(CommunityCatalogSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            return source.Url;
        }

        return RequireText(source.Label, $"source {source.Id}");
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{field} is required.");
        }

        return value;
    }

    private static string? NormalizeSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
