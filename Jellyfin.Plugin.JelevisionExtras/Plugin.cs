using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.JelevisionExtras.Services;

namespace Jellyfin.Plugin.JelevisionExtras;

/// <summary>
/// Enriches anonymous local movie extras with metadata from TheDiscDb.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Provider id key used to mark metadata managed by this plugin.
    /// </summary>
    public const string ProviderKey = "JelevisionExtras";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Jelevision Extras Enricher";

    /// <inheritdoc />
    public override string Description =>
        "Identifies anonymous local extras using TheDiscDb and the open Jelevision verified catalog.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("8b43a8c3-42ed-4fdf-8fb7-41d853b85ef4");

    /// <summary>
    /// Gets the active plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }
}

/// <summary>
/// Community catalog configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin downloads the open
    /// verified catalog. Matching remains local and sends no library ids.
    /// </summary>
    public bool EnableCommunityCatalog { get; set; } = true;

    /// <summary>
    /// Gets or sets the catalog JSON endpoint. A self-hosted snapshot may be
    /// used instead of the public GitHub endpoint.
    /// </summary>
    public string CommunityCatalogUrl { get; set; } =
        CommunityCatalogClient.DefaultCatalogUrl;
}
