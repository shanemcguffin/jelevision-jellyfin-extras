using System.Globalization;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JelevisionExtras.Matching;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JelevisionExtras.Overrides;

/// <summary>
/// Identifies the parent-id namespace used by a curated override.
/// </summary>
public enum ParentIdKind
{
    /// <summary>
    /// The Movie Database movie id.
    /// </summary>
    Tmdb,

    /// <summary>
    /// IMDb title id.
    /// </summary>
    Imdb
}

/// <summary>
/// Describes the safe action attached to an exact curated match.
/// </summary>
public enum CuratedOverrideAction
{
    /// <summary>
    /// Assign a human-readable title and Jellyfin extra type.
    /// </summary>
    SetMetadata,

    /// <summary>
    /// Remove the extra classification from a verified technical/legal reel.
    /// </summary>
    HideTechnical
}

/// <summary>
/// One evidence-backed, library-specific metadata rule.
/// </summary>
/// <param name="RuleId">Stable rule identifier used in undo state.</param>
/// <param name="ParentIdKind">Parent provider-id namespace.</param>
/// <param name="ParentId">Required parent provider id.</param>
/// <param name="TitleOrdinal">Required trailing MakeMKV title number.</param>
/// <param name="DurationSeconds">Expected local runtime.</param>
/// <param name="RuntimeToleranceSeconds">Maximum accepted runtime delta.</param>
/// <param name="Action">Metadata or technical-reel action.</param>
/// <param name="Title">Title to apply for metadata actions.</param>
/// <param name="ExtraType">Jellyfin type to apply for metadata actions.</param>
/// <param name="Source">Human-readable provenance.</param>
/// <param name="Reason">Evidence supporting the rule.</param>
public sealed record CuratedOverrideRule(
    string RuleId,
    ParentIdKind ParentIdKind,
    string ParentId,
    int TitleOrdinal,
    double DurationSeconds,
    double RuntimeToleranceSeconds,
    CuratedOverrideAction Action,
    string? Title,
    ExtraType? ExtraType,
    string Source,
    string Reason);

/// <summary>
/// One exact local item matched to a curated rule.
/// </summary>
/// <param name="Local">Local Jellyfin item.</param>
/// <param name="Rule">Matched curated rule.</param>
/// <param name="DurationDeltaSeconds">Absolute runtime difference.</param>
public sealed record CuratedOverrideAssignment(
    LocalExtra Local,
    CuratedOverrideRule Rule,
    double DurationDeltaSeconds);

/// <summary>
/// Provides tightly scoped overrides for titles whose public disc metadata is
/// absent or incomplete.
/// </summary>
public sealed partial class CuratedOverrideCatalog
{
    private const double RuntimeToleranceSeconds = 1.25;
    private const string LocalInspection =
        "Local disc-title inspection performed 2026-07-25";
    private const string DumbDvdTalk =
        "https://www.dvdtalk.com/reviews/19518/dumb-and-dumber-unrated-edition/";
    private const string DumbGroucho =
        "https://www.grouchoreviews.com/reviews/6334f8f75fe75d1811c62f04";
    private const string DumbHomeTheaterForum =
        "https://www.hometheaterforum.com/community/threads/htf-blu-ray-review-dumb-and-dumber.276381/";
    private const string ObsessionUniversal =
        "https://www.universalpicturesathome.com/press-release/obsession-press-release";

    private static readonly IReadOnlyList<CuratedOverrideRule> CatalogRules =
    [
        Hide(
            "it-1990-t00-technical",
            ParentIdKind.Imdb,
            "tt0099864",
            0,
            340.298,
            LocalInspection,
            "The title contains only international copyright-warning screens and has no program audio."),

        Hide(
            "obsession-2026-t00-technical",
            ParentIdKind.Tmdb,
            "1339713",
            0,
            325.325,
            LocalInspection,
            "Japanese hardware and legal disclaimer reel with no program audio."),
        Hide(
            "obsession-2026-t02-technical",
            ParentIdKind.Tmdb,
            "1339713",
            2,
            325.325,
            LocalInspection,
            "Japanese HDR disclaimer reel with no program audio."),
        Hide(
            "obsession-2026-t03-technical",
            ParentIdKind.Tmdb,
            "1339713",
            3,
            125.375,
            LocalInspection,
            "French copyright-warning reel with no program audio."),
        Hide(
            "obsession-2026-t04-technical",
            ParentIdKind.Tmdb,
            "1339713",
            4,
            330.580,
            LocalInspection,
            "Spanish interview-opinion disclaimer reel with no program audio."),
        Metadata(
            "obsession-2026-t05-featurette",
            ParentIdKind.Tmdb,
            "1339713",
            5,
            1153.026,
            "Obsession Unleashed",
            ExtraType.Featurette,
            ObsessionUniversal,
            "The studio press release identifies the standalone 19-minute video bonus by this title."),

        Hide(
            "dumb-and-dumber-1994-t00-technical",
            ParentIdKind.Tmdb,
            "8467",
            0,
            340.340,
            LocalInspection,
            "Warner international copyright-warning reel with no program audio."),
        Metadata(
            "dumb-and-dumber-1994-t02-additional-scenes",
            ParentIdKind.Tmdb,
            "8467",
            2,
            2035.433,
            "Additional Scenes — Play All",
            ExtraType.DeletedScene,
            DumbHomeTheaterForum,
            "The Blu-ray review identifies the 33:55 Additional Scenes program; local runtime and content match."),
        Metadata(
            "dumb-and-dumber-1994-t03-dumb-moments",
            ParentIdKind.Tmdb,
            "8467",
            3,
            465.598,
            "Deliriously Dumb Moments — Play All",
            ExtraType.Featurette,
            DumbGroucho,
            "The four documented scene retrospectives total 7:45, matching this play-all title."),
        Metadata(
            "dumb-and-dumber-1994-t04-retrospective",
            ParentIdKind.Tmdb,
            "8467",
            4,
            1115.147,
            "Still Dumb After All These Years",
            ExtraType.Featurette,
            DumbHomeTheaterForum,
            "The documented 18:35 retrospective exactly matches the local title."),
        Metadata(
            "dumb-and-dumber-1994-t05-trailer",
            ParentIdKind.Tmdb,
            "8467",
            5,
            129.152,
            "Theatrical Trailer",
            ExtraType.Trailer,
            LocalInspection,
            "The local title is the film's theatrical trailer; runtime and visual content were verified."),
        Metadata(
            "dumb-and-dumber-1994-t06-the-box",
            ParentIdKind.Tmdb,
            "8467",
            6,
            206.239,
            "The Box",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 3:27 deleted scene matches the local runtime and content."),
        Metadata(
            "dumb-and-dumber-1994-t07-cracker",
            ParentIdKind.Tmdb,
            "8467",
            7,
            122.155,
            "Hey, Get Me a Cracker!",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 2:01 deleted scene matches the local runtime and van-scene content."),
        Metadata(
            "dumb-and-dumber-1994-t08-somebody-elses-money",
            ParentIdKind.Tmdb,
            "8467",
            8,
            251.284,
            "Somebody Else's Money",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 4:08 deleted scene matches the local runtime and hotel-scene content."),
        Metadata(
            "dumb-and-dumber-1994-t09-short-scenes",
            ParentIdKind.Tmdb,
            "8467",
            9,
            324.357,
            "Deleted Short Scenes and Shot Montage with Jeff Daniels",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 5:30 montage matches the local runtime and Jeff Daniels introduction."),
        Metadata(
            "dumb-and-dumber-1994-t10-seabass",
            ParentIdKind.Tmdb,
            "8467",
            10,
            223.256,
            "Lloyd and Seabass",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 3:44 deleted scene matches the local runtime and gas-station content."),
        Metadata(
            "dumb-and-dumber-1994-t11-petey",
            ParentIdKind.Tmdb,
            "8467",
            11,
            283.316,
            "R.I.P. Petey",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 4:41 deleted scene matches the local runtime and bird-cage content."),
        Metadata(
            "dumb-and-dumber-1994-t12-alternate-ending",
            ParentIdKind.Tmdb,
            "8467",
            12,
            179.212,
            "Alternate Ending #1",
            ExtraType.DeletedScene,
            DumbDvdTalk,
            "The documented 2:58 alternate ending matches the local runtime and bellhop content."),
        Metadata(
            "dumb-and-dumber-1994-t13-toilet",
            ParentIdKind.Tmdb,
            "8467",
            13,
            140.173,
            "The Toilet Scene",
            ExtraType.Featurette,
            DumbGroucho,
            "The documented 2:20 scene retrospective exactly matches the local runtime and content."),
        Metadata(
            "dumb-and-dumber-1994-t14-fire-stunt",
            ParentIdKind.Tmdb,
            "8467",
            14,
            122.155,
            "Big Fire Stunt",
            ExtraType.Featurette,
            DumbGroucho,
            "The documented 2:02 stunt retrospective exactly matches the local runtime and content.")
    ];

    /// <summary>
    /// Gets all curated rules.
    /// </summary>
    public IReadOnlyList<CuratedOverrideRule> Rules => CatalogRules;

    /// <summary>
    /// Matches exact local title ordinals and runtimes under a strong parent id.
    /// </summary>
    /// <param name="tmdbId">Parent TMDb id, when known.</param>
    /// <param name="imdbId">Parent IMDb id, when known.</param>
    /// <param name="localExtras">Eligible local extras.</param>
    /// <returns>Safe curated assignments.</returns>
    public IReadOnlyList<CuratedOverrideAssignment> Match(
        string? tmdbId,
        string? imdbId,
        IReadOnlyList<LocalExtra> localExtras)
    {
        return Match(tmdbId, imdbId, localExtras, CatalogRules);
    }

    /// <summary>
    /// Matches local extras against a supplied verified catalog snapshot.
    /// </summary>
    /// <param name="tmdbId">Parent TMDb id, when known.</param>
    /// <param name="imdbId">Parent IMDb id, when known.</param>
    /// <param name="localExtras">Eligible local extras.</param>
    /// <param name="rules">Verified exact-match rules.</param>
    /// <returns>Safe verified assignments.</returns>
    public IReadOnlyList<CuratedOverrideAssignment> Match(
        string? tmdbId,
        string? imdbId,
        IReadOnlyList<LocalExtra> localExtras,
        IReadOnlyList<CuratedOverrideRule> rules)
    {
        var applicableRules = rules
            .Where(rule => ParentMatches(rule, tmdbId, imdbId))
            .ToList();
        if (applicableRules.Count == 0)
        {
            return [];
        }

        var assignments = new List<CuratedOverrideAssignment>();
        var assignedItems = new HashSet<Guid>();
        foreach (var rule in applicableRules)
        {
            var candidates = localExtras
                .Where(extra =>
                    TryGetTitleOrdinal(extra.FileName, out var ordinal)
                    && ordinal == rule.TitleOrdinal
                    && Math.Abs(extra.DurationSeconds - rule.DurationSeconds)
                        <= rule.RuntimeToleranceSeconds)
                .ToList();
            if (candidates.Count != 1 || !assignedItems.Add(candidates[0].Id))
            {
                continue;
            }

            assignments.Add(new CuratedOverrideAssignment(
                candidates[0],
                rule,
                Math.Abs(candidates[0].DurationSeconds - rule.DurationSeconds)));
        }

        return assignments;
    }

    /// <summary>
    /// Prefers downloaded verified records and fills any missing selectors
    /// from the plugin's bundled fallback snapshot.
    /// </summary>
    /// <param name="downloadedRules">Downloaded community rules.</param>
    /// <returns>A complete, selector-deduplicated rule list.</returns>
    public IReadOnlyList<CuratedOverrideRule> MergeWithFallback(
        IReadOnlyList<CuratedOverrideRule> downloadedRules)
    {
        var merged = new List<CuratedOverrideRule>();
        var selectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in downloadedRules.Concat(CatalogRules))
        {
            var selector = string.Join(
                '|',
                rule.ParentIdKind,
                rule.ParentId,
                rule.TitleOrdinal.ToString(CultureInfo.InvariantCulture),
                rule.DurationSeconds.ToString("R", CultureInfo.InvariantCulture));
            if (selectors.Add(selector))
            {
                merged.Add(rule);
            }
        }

        return merged;
    }

    private static bool ParentMatches(
        CuratedOverrideRule rule,
        string? tmdbId,
        string? imdbId)
    {
        var actual = rule.ParentIdKind == ParentIdKind.Tmdb
            ? tmdbId
            : imdbId;
        return string.Equals(actual, rule.ParentId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetTitleOrdinal(string fileName, out int ordinal)
    {
        ordinal = 0;
        var match = TitleOrdinalRegex().Match(fileName);
        return match.Success
            && int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ordinal);
    }

    private static CuratedOverrideRule Hide(
        string ruleId,
        ParentIdKind parentIdKind,
        string parentId,
        int titleOrdinal,
        double durationSeconds,
        string source,
        string reason)
    {
        return new CuratedOverrideRule(
            ruleId,
            parentIdKind,
            parentId,
            titleOrdinal,
            durationSeconds,
            RuntimeToleranceSeconds,
            CuratedOverrideAction.HideTechnical,
            null,
            null,
            source,
            reason);
    }

    private static CuratedOverrideRule Metadata(
        string ruleId,
        ParentIdKind parentIdKind,
        string parentId,
        int titleOrdinal,
        double durationSeconds,
        string title,
        ExtraType extraType,
        string source,
        string reason)
    {
        return new CuratedOverrideRule(
            ruleId,
            parentIdKind,
            parentId,
            titleOrdinal,
            durationSeconds,
            RuntimeToleranceSeconds,
            CuratedOverrideAction.SetMetadata,
            title,
            extraType,
            source,
            reason);
    }

    [GeneratedRegex(@"_t(\d+)(?:\.[^.]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleOrdinalRegex();
}
