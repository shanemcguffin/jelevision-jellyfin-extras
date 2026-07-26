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
        "Identifies anonymous local extras using TheDiscDb, the public Jelevision seed, and optional private catalog feeds.";

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
    /// Gets or sets a value indicating whether the plugin downloads the
    /// configured catalog. Matching remains local and sends no library ids.
    /// </summary>
    public bool EnableCommunityCatalog { get; set; } = true;

    /// <summary>
    /// Gets or sets the catalog JSON endpoint. The default contains only the
    /// public seed; a self-hosted or licensed feed may be used instead.
    /// </summary>
    public string CommunityCatalogUrl { get; set; } =
        CommunityCatalogClient.DefaultCatalogUrl;

    /// <summary>
    /// Gets or sets the optional Bearer token used for a private catalog feed.
    /// The JELEVISION_EXTRAS_CATALOG_TOKEN environment variable takes
    /// precedence when set.
    /// </summary>
    public string CommunityCatalogAccessToken { get; set; } = string.Empty;
}
