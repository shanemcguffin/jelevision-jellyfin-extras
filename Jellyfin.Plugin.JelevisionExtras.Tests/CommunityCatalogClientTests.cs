using System.Net;
using System.Text;
using Jellyfin.Plugin.JelevisionExtras.Overrides;
using Jellyfin.Plugin.JelevisionExtras.Services;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.JelevisionExtras.Tests;

public sealed class CommunityCatalogClientTests
{
    [Fact]
    public async Task DownloadsVerifiedRulesWithoutSendingLibraryIdentifiers()
    {
        var handler = new CatalogHandler(VerifiedCatalog);
        using var httpClient = new HttpClient(handler);
        var endpoint = new Uri("http://127.0.0.1:8787/catalog/v1/catalog.json");
        var client = CommunityCatalogClient.CreateForEndpoint(
            httpClient,
            NullLogger<CommunityCatalogClient>.Instance,
            endpoint);

        var snapshot = await client.GetVerifiedRulesAsync(CancellationToken.None);

        Assert.Equal("2026.07.25.1", snapshot.Version);
        Assert.Equal(1, snapshot.EntryCount);
        Assert.Equal(2, snapshot.Rules.Count);
        Assert.Contains(
            snapshot.Rules,
            rule =>
                rule.ParentIdKind == ParentIdKind.Tmdb
                && rule.ParentId == "8467"
                && rule.ExtraType == ExtraType.Trailer
                && rule.Title == "Theatrical Trailer");
        Assert.Equal(endpoint, handler.RequestUri);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task RejectsUnverifiedRecords()
    {
        using var httpClient = new HttpClient(
            new CatalogHandler(
                VerifiedCatalog.Replace(
                    "\"status\":\"verified\"",
                    "\"status\":\"proposed\"",
                    StringComparison.Ordinal)));
        var client = CommunityCatalogClient.CreateForEndpoint(
            httpClient,
            NullLogger<CommunityCatalogClient>.Instance,
            new Uri("https://catalog.invalid/catalog.json"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetVerifiedRulesAsync(
                CancellationToken.None));

        Assert.Contains("only verified", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DownloadedRulesOverrideEquivalentBundledSelectors()
    {
        var fallback = new CuratedOverrideCatalog();
        var replacement = new CuratedOverrideRule(
            "replacement",
            ParentIdKind.Tmdb,
            "8467",
            5,
            129.152,
            1.25,
            CuratedOverrideAction.SetMetadata,
            "Verified Trailer",
            ExtraType.Trailer,
            "community",
            "verified");

        var merged = fallback.MergeWithFallback([replacement]);
        var selected = Assert.Single(
            merged,
            rule =>
                rule.ParentIdKind == ParentIdKind.Tmdb
                && rule.ParentId == "8467"
                && rule.TitleOrdinal == 5
                && Math.Abs(rule.DurationSeconds - 129.152) < 0.001);

        Assert.Equal("replacement", selected.RuleId);
    }

    private const string VerifiedCatalog =
        """
        {
          "schemaVersion":1,
          "catalogVersion":"2026.07.25.1",
          "sources":[
            {"id":"inspection","label":"Local inspection"}
          ],
          "items":[
            {
              "id":"dumb-and-dumber-trailer",
              "media":{"ids":{"tmdb":"8467","imdb":"tt0109686"}},
              "match":{
                "titleOrdinal":5,
                "durationSeconds":129.152,
                "toleranceSeconds":1.25
              },
              "result":{
                "action":"set_metadata",
                "title":"Theatrical Trailer",
                "type":"trailer"
              },
              "verification":{
                "status":"verified",
                "sourceIds":["inspection"],
                "evidence":["Title card and runtime match."]
              }
            }
          ]
        }
        """;

    private sealed class CatalogHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public HttpMethod? Method { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            RequestBody = request.Content is null
                ? null
                : await request.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
