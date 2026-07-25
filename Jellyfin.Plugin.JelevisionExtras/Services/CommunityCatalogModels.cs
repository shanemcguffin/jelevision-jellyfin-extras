using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

internal sealed class CommunityCatalogDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("catalogVersion")]
    public string? CatalogVersion { get; set; }

    [JsonPropertyName("sources")]
    public List<CommunityCatalogSource> Sources { get; set; } = [];

    [JsonPropertyName("items")]
    public List<CommunityCatalogItem> Items { get; set; } = [];
}

internal sealed class CommunityCatalogSource
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class CommunityCatalogItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("media")]
    public CommunityCatalogMedia? Media { get; set; }

    [JsonPropertyName("match")]
    public CommunityCatalogMatch? Match { get; set; }

    [JsonPropertyName("result")]
    public CommunityCatalogResult? Result { get; set; }

    [JsonPropertyName("verification")]
    public CommunityCatalogVerification? Verification { get; set; }
}

internal sealed class CommunityCatalogMedia
{
    [JsonPropertyName("ids")]
    public CommunityCatalogMediaIds? Ids { get; set; }
}

internal sealed class CommunityCatalogMediaIds
{
    [JsonPropertyName("tmdb")]
    public string? Tmdb { get; set; }

    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }
}

internal sealed class CommunityCatalogMatch
{
    [JsonPropertyName("titleOrdinal")]
    public int TitleOrdinal { get; set; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("toleranceSeconds")]
    public double ToleranceSeconds { get; set; }
}

internal sealed class CommunityCatalogResult
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

internal sealed class CommunityCatalogVerification
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("sourceIds")]
    public List<string> SourceIds { get; set; } = [];

    [JsonPropertyName("evidence")]
    public List<string> Evidence { get; set; } = [];
}
