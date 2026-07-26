using System.Globalization;
using System.Text.RegularExpressions;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JelevisionExtras.Matching;
using Jellyfin.Plugin.JelevisionExtras.Overrides;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Video = MediaBrowser.Controller.Entities.Video;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

/// <summary>
/// Coordinates Jellyfin library reads, matching, metadata updates, reports, and undo.
/// </summary>
public sealed partial class ExtrasEnrichmentService
{
    private readonly ILibraryManager _libraryManager;
    private readonly TheDiscDbClient _discDbClient;
    private readonly CommunityCatalogClient _communityCatalogClient;
    private readonly ExtraMetadataMatcher _matcher;
    private readonly CuratedOverrideCatalog _overrideCatalog;
    private readonly EnrichmentStateStore _stateStore;
    private readonly ILogger<ExtrasEnrichmentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtrasEnrichmentService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="discDbClient">TheDiscDb client.</param>
    /// <param name="communityCatalogClient">Open verified catalog client.</param>
    /// <param name="matcher">Metadata matcher.</param>
    /// <param name="overrideCatalog">Evidence-backed local overrides.</param>
    /// <param name="stateStore">Undo/report store.</param>
    /// <param name="logger">Logger.</param>
    public ExtrasEnrichmentService(
        ILibraryManager libraryManager,
        TheDiscDbClient discDbClient,
        CommunityCatalogClient communityCatalogClient,
        ExtraMetadataMatcher matcher,
        CuratedOverrideCatalog overrideCatalog,
        EnrichmentStateStore stateStore,
        ILogger<ExtrasEnrichmentService> logger)
    {
        _libraryManager = libraryManager;
        _discDbClient = discDbClient;
        _communityCatalogClient = communityCatalogClient;
        _matcher = matcher;
        _overrideCatalog = overrideCatalog;
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <summary>
    /// Previews or applies confident extra metadata matches.
    /// </summary>
    /// <param name="apply">Whether to persist matched metadata.</param>
    /// <param name="progress">Task progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Run report.</returns>
    public async Task<EnrichmentRunReport> RunAsync(
        bool apply,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var report = new EnrichmentRunReport
        {
            Mode = apply ? "Apply" : "Preview",
            StartedAtUtc = startedAt
        };

        var movies = GetMoviesWithEligibleExtras();
        report.MoviesScanned = movies.Count;
        if (movies.Count == 0)
        {
            report.CompletedAtUtc = DateTime.UtcNow;
            await _stateStore.WriteReportAsync(report, cancellationToken).ConfigureAwait(false);
            progress.Report(100);
            return report;
        }

        IReadOnlyList<CuratedOverrideRule> exactRules = _overrideCatalog.Rules;
        var configuration = Plugin.Instance?.Configuration
            ?? new PluginConfiguration();
        if (configuration.EnableCommunityCatalog)
        {
            try
            {
                var communityCatalog = await _communityCatalogClient
                    .GetVerifiedRulesAsync(cancellationToken)
                    .ConfigureAwait(false);
                exactRules = _overrideCatalog.MergeWithFallback(
                    communityCatalog.Rules);
                report.CommunityCatalogVersion = communityCatalog.Version;
                report.CommunityCatalogEntries = communityCatalog.EntryCount;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                report.CommunityCatalogError = exception.Message;
                _logger.LogWarning(
                    exception,
                    "The Jelevision community catalog was unavailable; continuing with the bundled verified snapshot");
            }
        }

        var tmdbIds = movies
            .Select(movie => movie.GetProviderId(MetadataProvider.Tmdb))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
        IReadOnlyDictionary<string, IReadOnlyList<DiscCandidate>> catalog;
        try
        {
            catalog = await _discDbClient
                .GetCandidatesAsync(tmdbIds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            report.CatalogError = exception.Message;
            catalog = new Dictionary<string, IReadOnlyList<DiscCandidate>>(
                StringComparer.Ordinal);
            _logger.LogWarning(
                exception,
                "TheDiscDb was unavailable; continuing with curated overrides");
        }

        for (var index = 0; index < movies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var movie = movies[index];
            var tmdbId = movie.GetProviderId(MetadataProvider.Tmdb) ?? string.Empty;
            var imdbId = movie.GetProviderId(MetadataProvider.Imdb) ?? string.Empty;
            var videos = GetEligibleExtras(movie);
            var localExtras = videos
                .Select(video => new LocalExtra(
                    video.Id,
                    video.Name,
                    Path.GetFileName(video.Path) ?? video.Name,
                    video.RunTimeTicks.GetValueOrDefault() / 10_000_000d,
                    video.ExtraType,
                    video.ProviderIds.ContainsKey(Plugin.ProviderKey)))
                .ToList();

            var curatedAssignments = _overrideCatalog.Match(
                tmdbId,
                imdbId,
                localExtras,
                exactRules);
            var curatedItemIds = curatedAssignments
                .Select(assignment => assignment.Local.Id)
                .ToHashSet();
            var unresolvedExtras = localExtras
                .Where(extra => !curatedItemIds.Contains(extra.Id))
                .ToList();
            var candidates = catalog.TryGetValue(tmdbId, out var found)
                ? found
                : [];
            var match = unresolvedExtras.Count == 0
                ? new MovieMatchResult(
                    true,
                    "Every eligible item matched an exact verified catalog rule.",
                    null,
                    [])
                : _matcher.Match(unresolvedExtras, candidates);
            var itemLookup = videos.ToDictionary(video => video.Id);
            var itemReports = new List<ExtraMatchReport>();

            foreach (var assignment in curatedAssignments)
            {
                var changed = false;
                if (apply
                    && itemLookup.TryGetValue(assignment.Local.Id, out var video))
                {
                    changed = await ApplyCuratedAssignmentAsync(
                        movie,
                        video,
                        assignment,
                        cancellationToken).ConfigureAwait(false);
                }

                var isTechnical =
                    assignment.Rule.Action == CuratedOverrideAction.HideTechnical;
                var isDuplicate =
                    assignment.Rule.Action == CuratedOverrideAction.HideDuplicate;
                var appliedTitle = assignment.Rule.Title ?? assignment.Local.Name;
                itemReports.Add(new ExtraMatchReport(
                    assignment.Local.Id,
                    assignment.Local.Name,
                    appliedTitle,
                    assignment.Rule.ExtraType,
                    assignment.Rule.Action.ToString(),
                    assignment.Rule.Source,
                    assignment.Rule.Reason,
                    assignment.DurationDeltaSeconds,
                    true,
                    changed));

                if (isTechnical)
                {
                    report.TechnicalItemsIdentified++;
                    if (changed)
                    {
                        report.TechnicalItemsChanged++;
                    }
                }
                else if (isDuplicate)
                {
                    report.DuplicateItemsIdentified++;
                    if (changed)
                    {
                        report.DuplicateItemsChanged++;
                    }
                }
                else
                {
                    report.CuratedMetadataMatches++;
                    report.ExtrasMatched++;
                }

                if (changed)
                {
                    report.ExtrasChanged++;
                }
            }

            foreach (var assignment in match.Assignments)
            {
                var changed = false;
                if (apply && match.IsConfident
                    && itemLookup.TryGetValue(assignment.Local.Id, out var video))
                {
                    changed = await ApplyAssignmentAsync(
                        movie,
                        video,
                        tmdbId,
                        match.Candidate!,
                        assignment,
                        cancellationToken).ConfigureAwait(false);
                }

                itemReports.Add(new ExtraMatchReport(
                    assignment.Local.Id,
                    assignment.Local.Name,
                    assignment.Disc.Title,
                    assignment.ExtraType,
                    CuratedOverrideAction.SetMetadata.ToString(),
                    "https://thediscdb.com/",
                    match.Reason,
                    assignment.DurationDeltaSeconds,
                    match.IsConfident,
                    changed));
                if (changed)
                {
                    report.ExtrasChanged++;
                }
            }

            if (match.IsConfident)
            {
                report.ExtrasMatched += match.Assignments.Count;
            }

            var automaticAssignments = curatedAssignments.Count
                + (match.IsConfident ? match.Assignments.Count : 0);
            var fullyResolved = automaticAssignments == localExtras.Count;
            if (automaticAssignments > 0)
            {
                report.MoviesMatched++;
            }

            var outcome = GetOutcome(
                curatedAssignments.Count,
                match.IsConfident ? match.Assignments.Count : 0,
                fullyResolved,
                match.Assignments.Count);
            var reason = curatedAssignments.Count > 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{curatedAssignments.Count} exact verified catalog assignment(s). {match.Reason}")
                : match.Reason;
            report.Movies.Add(new MovieEnrichmentReport(
                movie.Id,
                movie.Name,
                tmdbId,
                imdbId,
                fullyResolved,
                outcome,
                reason,
                match.Candidate?.ReleaseTitle,
                match.Candidate?.DiscName,
                itemReports));

            _logger.LogInformation(
                "{Mode} extras for {Movie}: outcome={Outcome}, resolved={Resolved}/{Eligible}, reason={Reason}",
                report.Mode,
                movie.Name,
                outcome,
                automaticAssignments,
                localExtras.Count,
                reason);

            progress.Report((index + 1d) / movies.Count * 100d);
        }

        report.CompletedAtUtc = DateTime.UtcNow;
        await _stateStore.WriteReportAsync(report, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "{Mode} complete: {MatchedMovies}/{ScannedMovies} movies had safe assignments, {MatchedExtras} metadata matches, {TechnicalItems} technical items, {ChangedExtras} changed",
            report.Mode,
            report.MoviesMatched,
            report.MoviesScanned,
            report.ExtrasMatched,
            report.TechnicalItemsIdentified,
            report.ExtrasChanged);
        return report;
    }

    /// <summary>
    /// Reverts plugin-managed metadata when it has not subsequently been edited.
    /// </summary>
    /// <param name="progress">Task progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of reverted extras.</returns>
    public async Task<int> UndoAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var states = await _stateStore.ReadAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (states.Count == 0)
        {
            progress.Report(100);
            return 0;
        }

        var revertedIds = new List<Guid>();
        for (var index = 0; index < states.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = states[index];
            var video = _libraryManager.GetItemById<Video>(state.ItemId);
            var owner = _libraryManager.GetItemById<Movie>(state.OwnerId);
            if (video is null || owner is null)
            {
                progress.Report((index + 1d) / states.Count * 100d);
                continue;
            }

            var isUnchangedSinceApply =
                string.Equals(video.Name, state.AppliedName, StringComparison.Ordinal)
                && video.ExtraType == state.AppliedExtraType
                && video.ProviderIds.TryGetValue(Plugin.ProviderKey, out var source)
                && string.Equals(source, state.SourceId, StringComparison.Ordinal);
            if (!isUnchangedSinceApply)
            {
                _logger.LogWarning(
                    "Skipping undo for {Extra}; its metadata changed after enrichment",
                    video.Name);
                progress.Report((index + 1d) / states.Count * 100d);
                continue;
            }

            video.Name = state.OriginalName;
            video.ExtraType = state.OriginalExtraType;
            video.ProviderIds.Remove(Plugin.ProviderKey);
            await _libraryManager.UpdateItemAsync(
                video,
                owner,
                ItemUpdateType.MetadataEdit,
                cancellationToken).ConfigureAwait(false);
            revertedIds.Add(video.Id);
            progress.Report((index + 1d) / states.Count * 100d);
        }

        if (revertedIds.Count > 0)
        {
            await _stateStore.RemoveAppliedAsync(revertedIds, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Reverted {RevertedCount} plugin-managed extra metadata items",
            revertedIds.Count);
        return revertedIds.Count;
    }

    private List<Movie> GetMoviesWithEligibleExtras()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Movie],
                IsVirtualItem = false,
                Recursive = true,
                GroupByPresentationUniqueKey = false
            })
            .OfType<Movie>()
            .Where(movie => GetEligibleExtras(movie).Count > 0)
            .OrderBy(movie => movie.SortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<Video> GetEligibleExtras(Movie movie)
    {
        return movie.GetExtras()
            .OfType<Video>()
            .Where(video =>
                video.RunTimeTicks.GetValueOrDefault() > 0
                && (video.ExtraType is null or ExtraType.Unknown
                    || video.ProviderIds.ContainsKey(Plugin.ProviderKey))
                && (video.ProviderIds.ContainsKey(Plugin.ProviderKey)
                    || LooksGeneric(video, movie.Name)))
            .OrderBy(video => video.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool LooksGeneric(Video video, string movieName)
    {
        var fileStem = Path.GetFileNameWithoutExtension(video.Path) ?? string.Empty;
        if (string.Equals(video.Name, fileStem, StringComparison.OrdinalIgnoreCase)
            && TitleOrdinalRegex().IsMatch(fileStem))
        {
            return true;
        }

        if (TitleOrdinalRegex().IsMatch(video.Name))
        {
            return true;
        }

        var normalizedName = NormalizeGenericName(video.Name);
        var normalizedMovie = NormalizeGenericName(movieName);
        return normalizedName.Equals("EXTRA", StringComparison.Ordinal)
            || normalizedName.Equals("BONUSFEATURE", StringComparison.Ordinal)
            || (normalizedName.StartsWith(normalizedMovie, StringComparison.Ordinal)
                && TrailingNumberRegex().IsMatch(normalizedName[normalizedMovie.Length..]));
    }

    private static string NormalizeGenericName(string value)
    {
        return NonAlphaNumericRegex().Replace(value, string.Empty).ToUpperInvariant();
    }

    private async Task<bool> ApplyAssignmentAsync(
        Movie movie,
        Video video,
        string tmdbId,
        DiscCandidate candidate,
        ExtraAssignment assignment,
        CancellationToken cancellationToken)
    {
        var sourceId = string.Join(
            '|',
            "v1",
            tmdbId,
            candidate.ReleaseSlug,
            candidate.DiscSlug,
            assignment.Disc.Index.ToString(CultureInfo.InvariantCulture),
            assignment.Disc.SourceFile ?? string.Empty);

        return await ApplyMetadataAsync(
            movie,
            video,
            assignment.Disc.Title,
            assignment.ExtraType,
            sourceId,
            string.Create(
                CultureInfo.InvariantCulture,
                $"TheDiscDb runtime match, delta {assignment.DurationDeltaSeconds:0.000}s"),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ApplyCuratedAssignmentAsync(
        Movie movie,
        Video video,
        CuratedOverrideAssignment assignment,
        CancellationToken cancellationToken)
    {
        var appliedName = assignment.Rule.Title ?? video.Name;
        var appliedType =
            assignment.Rule.Action is
                CuratedOverrideAction.HideTechnical
                or CuratedOverrideAction.HideDuplicate
                ? null
                : assignment.Rule.ExtraType;
        var sourceId = string.Join('|', "v3", "catalog", assignment.Rule.RuleId);

        return await ApplyMetadataAsync(
            movie,
            video,
            appliedName,
            appliedType,
            sourceId,
            assignment.Rule.Reason,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ApplyMetadataAsync(
        Movie movie,
        Video video,
        string appliedName,
        ExtraType? appliedType,
        string sourceId,
        string reason,
        CancellationToken cancellationToken)
    {
        var hasChanged =
            !string.Equals(video.Name, appliedName, StringComparison.Ordinal)
            || video.ExtraType != appliedType
            || !video.ProviderIds.TryGetValue(Plugin.ProviderKey, out var currentSource)
            || !string.Equals(currentSource, sourceId, StringComparison.Ordinal);
        if (!hasChanged)
        {
            return false;
        }

        var state = new AppliedEnrichment(
            video.Id,
            movie.Id,
            video.Name,
            video.ExtraType,
            appliedName,
            appliedType,
            sourceId,
            DateTime.UtcNow);

        video.Name = appliedName;
        video.ExtraType = appliedType;
        video.ProviderIds[Plugin.ProviderKey] = sourceId;
        await _libraryManager.UpdateItemAsync(
            video,
            movie,
            ItemUpdateType.MetadataEdit,
            cancellationToken).ConfigureAwait(false);
        await _stateStore.RecordAppliedAsync(state, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated {Movie} extra {OldName} as {NewName} ({ExtraType}): {Reason}",
            movie.Name,
            state.OriginalName,
            state.AppliedName,
            state.AppliedExtraType,
            reason);
        return true;
    }

    private static string GetOutcome(
        int curatedAssignments,
        int catalogAssignments,
        bool fullyResolved,
        int tentativeCatalogAssignments)
    {
        if (fullyResolved && curatedAssignments > 0 && catalogAssignments > 0)
        {
            return "VerifiedCatalogAndTheDiscDb";
        }

        if (fullyResolved && curatedAssignments > 0)
        {
            return "VerifiedCatalog";
        }

        if (fullyResolved && catalogAssignments > 0)
        {
            return "TheDiscDb";
        }

        if (curatedAssignments > 0)
        {
            return "PartiallyVerifiedCatalog";
        }

        return tentativeCatalogAssignments > 0
            ? "IncompleteTheDiscDb"
            : "NoMatch";
    }

    [GeneratedRegex(@"_t\d+(?:\.[^.]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleOrdinalRegex();

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"[^A-Z0-9]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();
}
