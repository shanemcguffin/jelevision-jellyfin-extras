using Jellyfin.Plugin.JelevisionExtras.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.JelevisionExtras.ScheduledTasks;

/// <summary>
/// Reverts plugin-managed metadata.
/// </summary>
public sealed class UndoExtrasTask : IScheduledTask
{
    private readonly ExtrasEnrichmentService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoExtrasTask"/> class.
    /// </summary>
    /// <param name="service">Enrichment service.</param>
    public UndoExtrasTask(ExtrasEnrichmentService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Undo local extras enrichment";

    /// <inheritdoc />
    public string Key => "JelevisionExtrasUndo";

    /// <inheritdoc />
    public string Description =>
        "Restores metadata saved before enrichment when it has not subsequently been edited.";

    /// <inheritdoc />
    public string Category => "Jelevision";

    /// <inheritdoc />
    public async Task ExecuteAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        _ = await _service.UndoAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }
}
