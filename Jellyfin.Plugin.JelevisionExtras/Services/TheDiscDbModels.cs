using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

internal sealed class GraphQlEnvelope<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<GraphQlError>? Errors { get; init; }
}

internal sealed class GraphQlError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

internal sealed class ExtrasCatalogData
{
    [JsonPropertyName("mediaItems")]
    public MediaItemConnection? MediaItems { get; init; }
}

internal sealed class MediaItemConnection
{
    [JsonPropertyName("nodes")]
    public IReadOnlyList<DiscDbMediaItem> Nodes { get; init; } = [];
}

internal sealed class DiscDbMediaItem
{
    [JsonPropertyName("externalids")]
    public DiscDbExternalIds? ExternalIds { get; init; }

    [JsonPropertyName("releases")]
    public IReadOnlyList<DiscDbRelease> Releases { get; init; } = [];
}

internal sealed class DiscDbExternalIds
{
    [JsonPropertyName("tmdb")]
    public string? Tmdb { get; init; }
}

internal sealed class DiscDbRelease
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("discs")]
    public IReadOnlyList<DiscDbDisc> Discs { get; init; } = [];
}

internal sealed class DiscDbDisc
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("contentHash")]
    public string? ContentHash { get; init; }

    [JsonPropertyName("titles")]
    public IReadOnlyList<DiscDbTitle> Titles { get; init; } = [];
}

internal sealed class DiscDbTitle
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }

    [JsonPropertyName("duration")]
    public string? Duration { get; init; }

    [JsonPropertyName("item")]
    public DiscDbItem? Item { get; init; }
}

internal sealed class DiscDbItem
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}
