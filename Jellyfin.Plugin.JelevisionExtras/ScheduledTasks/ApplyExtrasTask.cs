using Jellyfin.Plugin.JelevisionExtras.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.JelevisionExtras.ScheduledTasks;

/// <summary>
/// Applies unique, high-confidence metadata matches.
/// </summary>
public sealed class ApplyExtrasTask : IScheduledTask
{
    private readonly ExtrasEnrichmentService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyExtrasTask"/> class.
    /// </summary>
    /// <param name="service">Enrichment service.</param>
    public ApplyExtrasTask(ExtrasEnrichmentService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Enrich local extras";

    /// <inheritdoc />
    public string Key => "JelevisionExtrasApply";

    /// <inheritdoc />
    public string Description =>
        "Applies guarded TheDiscDb or exact curated matches and records an undo snapshot.";

    /// <inheritdoc />
    public string Category => "Jelevision";

    /// <inheritdoc />
    public Task ExecuteAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        return _service.RunAsync(true, progress, cancellationToken);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
        };
    }
}
