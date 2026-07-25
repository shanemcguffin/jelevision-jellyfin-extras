using System.Text.Json;
using System.Text.Json.Serialization;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.JelevisionExtras.Services;

/// <summary>
/// Stores undo state and the most recent preview/apply report.
/// </summary>
public sealed class EnrichmentStateStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath;
    private readonly string _reportPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrichmentStateStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    public EnrichmentStateStore(IApplicationPaths applicationPaths)
    {
        _statePath = Path.Combine(
            applicationPaths.PluginConfigurationsPath,
            "JelevisionExtrasEnricher.state.json");
        _reportPath = Path.Combine(
            applicationPaths.PluginConfigurationsPath,
            "JelevisionExtrasEnricher.last-report.json");
    }

    /// <summary>
    /// Gets a snapshot of applied states.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Applied states.</returns>
    public async Task<IReadOnlyList<AppliedEnrichment>> ReadAppliedAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAppliedCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Adds or updates an applied-state record while preserving its original values.
    /// </summary>
    /// <param name="value">New state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RecordAppliedAsync(
        AppliedEnrichment value,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var states = (await ReadAppliedCoreAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(state => state.ItemId);

            if (states.TryGetValue(value.ItemId, out var existing))
            {
                value = value with
                {
                    OriginalName = existing.OriginalName,
                    OriginalExtraType = existing.OriginalExtraType
                };
            }

            states[value.ItemId] = value;
            await WriteAtomicAsync(
                _statePath,
                states.Values.OrderBy(state => state.ItemId).ToList(),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Removes successfully reverted records.
    /// </summary>
    /// <param name="itemIds">Ids to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RemoveAppliedAsync(
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ids = itemIds.ToHashSet();
            var remaining = (await ReadAppliedCoreAsync(cancellationToken).ConfigureAwait(false))
                .Where(state => !ids.Contains(state.ItemId))
                .ToList();
            await WriteAtomicAsync(_statePath, remaining, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Writes the latest run report.
    /// </summary>
    /// <param name="report">Report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteReportAsync(
        EnrichmentRunReport report,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAtomicAsync(_reportPath, report, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task<IReadOnlyList<AppliedEnrichment>> ReadAppliedCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_statePath);
        return await JsonSerializer.DeserializeAsync<List<AppliedEnrichment>>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static async Task WriteAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("State path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         8192,
                         FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                value,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, path, true);
    }
}
