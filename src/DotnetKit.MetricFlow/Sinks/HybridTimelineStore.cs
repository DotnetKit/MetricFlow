using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// A hybrid timeline store coordinating an ultra-fast in-memory ring buffer for recent queries
/// with durable file-based persistence for historical timeline queries across arbitrarily large periods.
/// </summary>
public sealed class HybridTimelineStore : IMetricTimelineStore
{
    private readonly HybridTimelineStoreOptions _options;
    private readonly MemoryRingBufferTimelineStore _memoryStore;
    private readonly FileTimelineStore? _fileStore;

    public string Name { get; }

    public MemoryRingBufferTimelineStore MemoryStore => _memoryStore;
    public FileTimelineStore? FileStore => _fileStore;

    public HybridTimelineStore(HybridTimelineStoreOptions? options = null, string name = "HybridTimelineStore")
    {
        _options = options ?? new HybridTimelineStoreOptions();
        Name = name;

        _memoryStore = new MemoryRingBufferTimelineStore(
            name: $"{name}_Memory",
            maxCapacity: _options.MemoryCapacity,
            retention: _options.MemoryRetention
        );

        if (_options.EnableFilePersistence)
        {
            _fileStore = new FileTimelineStore(_options.FileOptions, $"{name}_File");
        }
    }

    public async ValueTask EmitAsync(IReadOnlyList<MetricTimelineEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        // Always emit to in-memory store
        await _memoryStore.EmitAsync(entries, cancellationToken).ConfigureAwait(false);

        // Emit to file store if enabled
        if (_fileStore != null)
        {
            await _fileStore.EmitAsync(entries, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<IReadOnlyList<MetricTimelineEntry>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var memoryCutoff = DateTimeOffset.UtcNow.Subtract(_options.MemoryRetention);

        // If the query is completely within the in-memory retention window, serve from RAM (zero disk I/O)
        if (fromUtc >= memoryCutoff || _fileStore == null)
        {
            return await _memoryStore.GetTimelineAsync(fromUtc, toUtc, metricName, cancellationToken).ConfigureAwait(false);
        }

        // Otherwise, read from the file store which contains full historical data
        return await _fileStore.GetTimelineAsync(fromUtc, toUtc, metricName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<IMetricSnapshot>> GetAggregatedSnapshotsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var timeline = await GetTimelineAsync(fromUtc, toUtc, metricName, cancellationToken).ConfigureAwait(false);
        var deltas = timeline.Select(t => t.Delta).ToList();
        return deltas.AggregateSnapshots();
    }
}
