using Jellyfin.Plugin.JelevisionExtras.Matching;
using Jellyfin.Plugin.JelevisionExtras.Overrides;
using Jellyfin.Plugin.JelevisionExtras.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JelevisionExtras;

/// <summary>
/// Registers services used by the plugin's scheduled tasks.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<TheDiscDbClient>(client =>
        {
            client.BaseAddress = new Uri("https://thediscdb.com/");
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Jelevision-Extras-Enricher/0.3 (+https://github.com/TheDiscDb/data)");
        });

        serviceCollection.AddHttpClient<CommunityCatalogClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Jelevision-Extras-Enricher/0.3 (+https://github.com/shanemcguffin/jelevision-extras-catalog)");
        });

        serviceCollection.AddSingleton<ExtraMetadataMatcher>();
        serviceCollection.AddSingleton<CuratedOverrideCatalog>();
        serviceCollection.AddSingleton<EnrichmentStateStore>();
        serviceCollection.AddSingleton<ExtrasEnrichmentService>();
    }
}
