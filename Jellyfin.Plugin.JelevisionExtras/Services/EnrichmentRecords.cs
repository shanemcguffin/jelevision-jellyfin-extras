using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

/// <summary>
/// Persisted record that makes an enrichment update reversible.
/// </summary>
/// <param name="ItemId">Jellyfin extra item id.</param>
/// <param name="OwnerId">Parent movie id.</param>
/// <param name="OriginalName">Name before enrichment.</param>
/// <param name="OriginalExtraType">Extra type before enrichment.</param>
/// <param name="AppliedName">Name written by the plugin.</param>
/// <param name="AppliedExtraType">Extra type written by the plugin.</param>
/// <param name="SourceId">Stable source identifier.</param>
/// <param name="AppliedAtUtc">UTC application time.</param>
public sealed record AppliedEnrichment(
    Guid ItemId,
    Guid OwnerId,
    string OriginalName,
    ExtraType? OriginalExtraType,
    string AppliedName,
    ExtraType? AppliedExtraType,
    string SourceId,
    DateTime AppliedAtUtc);

/// <summary>
/// One reported extra match.
/// </summary>
/// <param name="ItemId">Jellyfin item id.</param>
/// <param name="OriginalName">Name seen during the run.</param>
/// <param name="MatchedTitle">Title that would be or was applied.</param>
/// <param name="MatchedType">Jellyfin type that would be or was applied.</param>
/// <param name="Action">Action selected for the item.</param>
/// <param name="Source">Metadata provenance.</param>
/// <param name="Reason">Evidence or matching explanation.</param>
/// <param name="DurationDeltaSeconds">Runtime difference.</param>
/// <param name="Automatic">Whether the action passed automatic safeguards.</param>
/// <param name="Changed">Whether this run changed the item.</param>
public sealed record ExtraMatchReport(
    Guid ItemId,
    string OriginalName,
    string MatchedTitle,
    ExtraType? MatchedType,
    string Action,
    string Source,
    string Reason,
    double DurationDeltaSeconds,
    bool Automatic,
    bool Changed);

/// <summary>
/// One movie in an enrichment report.
/// </summary>
/// <param name="MovieId">Jellyfin movie id.</param>
/// <param name="MovieName">Movie name.</param>
/// <param name="TmdbId">TMDb id.</param>
/// <param name="ImdbId">IMDb id.</param>
/// <param name="FullyResolved">Whether every eligible item has a safe assignment.</param>
/// <param name="Outcome">Compact result classification.</param>
/// <param name="Reason">Decision explanation.</param>
/// <param name="Release">Selected release.</param>
/// <param name="Disc">Selected disc.</param>
/// <param name="Matches">Extra assignments.</param>
public sealed record MovieEnrichmentReport(
    Guid MovieId,
    string MovieName,
    string TmdbId,
    string ImdbId,
    bool FullyResolved,
    string Outcome,
    string Reason,
    string? Release,
    string? Disc,
    IReadOnlyList<ExtraMatchReport> Matches);

/// <summary>
/// Complete scheduled-task report.
/// </summary>
public sealed class EnrichmentRunReport
{
    /// <summary>
    /// Gets or sets the run mode.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC start time.
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC completion time.
    /// </summary>
    public DateTime CompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of movies considered.
    /// </summary>
    public int MoviesScanned { get; set; }

    /// <summary>
    /// Gets or sets the number of confident movie matches.
    /// </summary>
    public int MoviesMatched { get; set; }

    /// <summary>
    /// Gets or sets the number of matched extras.
    /// </summary>
    public int ExtrasMatched { get; set; }

    /// <summary>
    /// Gets or sets the number of changed extras.
    /// </summary>
    public int ExtrasChanged { get; set; }

    /// <summary>
    /// Gets or sets the number of metadata assignments supplied by curated rules.
    /// </summary>
    public int CuratedMetadataMatches { get; set; }

    /// <summary>
    /// Gets or sets the number of verified technical/legal titles identified.
    /// </summary>
    public int TechnicalItemsIdentified { get; set; }

    /// <summary>
    /// Gets or sets the number of technical/legal titles hidden during an apply run.
    /// </summary>
    public int TechnicalItemsChanged { get; set; }

    /// <summary>
    /// Gets or sets an error returned by the remote catalog, if it was unavailable.
    /// </summary>
    public string? CatalogError { get; set; }

    /// <summary>
    /// Gets or sets the downloaded Jelevision community catalog version.
    /// </summary>
    public string? CommunityCatalogVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of verified entries in the downloaded catalog.
    /// </summary>
    public int CommunityCatalogEntries { get; set; }

    /// <summary>
    /// Gets or sets an error returned while downloading or validating the
    /// community catalog. The bundled snapshot remains available on failure.
    /// </summary>
    public string? CommunityCatalogError { get; set; }

    /// <summary>
    /// Gets the per-movie results.
    /// </summary>
    public List<MovieEnrichmentReport> Movies { get; } = [];
}
