using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JelevisionExtras.Matching;

/// <summary>
/// A local Jellyfin extra to identify.
/// </summary>
/// <param name="Id">Jellyfin item id.</param>
/// <param name="Name">Current item name.</param>
/// <param name="FileName">Current file name.</param>
/// <param name="DurationSeconds">Exact runtime in seconds.</param>
/// <param name="CurrentType">Current Jellyfin extra type.</param>
/// <param name="IsManaged">Whether the plugin previously managed this item.</param>
public sealed record LocalExtra(
    Guid Id,
    string Name,
    string FileName,
    double DurationSeconds,
    ExtraType? CurrentType,
    bool IsManaged);

/// <summary>
/// An identified title on a physical disc.
/// </summary>
/// <param name="Index">MakeMKV title index.</param>
/// <param name="Comment">Original MakeMKV output filename, when available.</param>
/// <param name="SourceFile">Blu-ray playlist or stream filename.</param>
/// <param name="DurationSeconds">Disc title runtime in seconds.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="DiscDbType">TheDiscDb item type.</param>
public sealed record DiscExtra(
    int Index,
    string? Comment,
    string? SourceFile,
    double DurationSeconds,
    string Title,
    string DiscDbType);

/// <summary>
/// One candidate physical disc.
/// </summary>
/// <param name="TmdbId">Parent movie TMDb id.</param>
/// <param name="ReleaseSlug">TheDiscDb release slug.</param>
/// <param name="ReleaseTitle">Release display title.</param>
/// <param name="DiscSlug">TheDiscDb disc slug.</param>
/// <param name="DiscName">Disc display name.</param>
/// <param name="ContentHash">TheDiscDb content hash, when known.</param>
/// <param name="Extras">Identified extras on the disc.</param>
public sealed record DiscCandidate(
    string TmdbId,
    string ReleaseSlug,
    string ReleaseTitle,
    string DiscSlug,
    string DiscName,
    string? ContentHash,
    IReadOnlyList<DiscExtra> Extras);

/// <summary>
/// A confident local-to-disc assignment.
/// </summary>
/// <param name="Local">Local Jellyfin item.</param>
/// <param name="Disc">Matched TheDiscDb title.</param>
/// <param name="ExtraType">Mapped Jellyfin extra type.</param>
/// <param name="DurationDeltaSeconds">Absolute runtime difference.</param>
public sealed record ExtraAssignment(
    LocalExtra Local,
    DiscExtra Disc,
    ExtraType ExtraType,
    double DurationDeltaSeconds);

/// <summary>
/// Result of matching all local extras for one movie.
/// </summary>
/// <param name="IsConfident">Whether metadata may be applied automatically.</param>
/// <param name="Reason">Human-readable decision reason.</param>
/// <param name="Candidate">Selected disc candidate, if any.</param>
/// <param name="Assignments">Selected assignments.</param>
public sealed record MovieMatchResult(
    bool IsConfident,
    string Reason,
    DiscCandidate? Candidate,
    IReadOnlyList<ExtraAssignment> Assignments);
