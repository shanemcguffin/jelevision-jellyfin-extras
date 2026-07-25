using Jellyfin.Plugin.JelevisionExtras.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.JelevisionExtras.ScheduledTasks;

/// <summary>
/// Previews enrichment without changing Jellyfin metadata.
/// </summary>
public sealed class PreviewExtrasTask : IScheduledTask
{
    private readonly ExtrasEnrichmentService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewExtrasTask"/> class.
    /// </summary>
    /// <param name="service">Enrichment service.</param>
    public PreviewExtrasTask(ExtrasEnrichmentService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Preview local extras enrichment";

    /// <inheritdoc />
    public string Key => "JelevisionExtrasPreview";

    /// <inheritdoc />
    public string Description =>
        "Previews guarded TheDiscDb and exact curated extra metadata without changing Jellyfin.";

    /// <inheritdoc />
    public string Category => "Jelevision";

    /// <inheritdoc />
    public Task ExecuteAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        return _service.RunAsync(false, progress, cancellationToken);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }
}
