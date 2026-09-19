using System.Collections.Concurrent;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// An in-memory circular buffer timeline store that holds recent metric snapshots for fast range queries.
/// </summary>
public sealed class MemoryRingBufferTimelineStore : IMetricTimelineStore
{
    private readonly int _maxCapacity;
    private readonly TimeSpan _retention;
    private readonly ConcurrentQueue<MetricTimelineEntry> _queue = new();
    private readonly object _trimLock = new();

    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MemoryRingBufferTimelineStore"/>.
    /// </summary>
    /// <param name="name">The name of the store.</param>
    /// <param name="maxCapacity">The maximum number of entries to retain in memory (default 10,000).</param>
    /// <param name="retention">The maximum duration to retain entries in memory (default 1 hour).</param>
    public MemoryRingBufferTimelineStore(
        string name = "MemoryTimelineStore",
        int maxCapacity = 10_000,
        TimeSpan? retention = null)
    {
        Name = name;
        _maxCapacity = maxCapacity > 0 ? maxCapacity : 10_000;
        _retention = retention ?? TimeSpan.FromHours(1);
    }

    public int Count => _queue.Count;

    public ValueTask EmitAsync(IReadOnlyList<MetricTimelineEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var entry in entries)
        {
            _queue.Enqueue(entry);
        }

        TrimExcess();
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<MetricTimelineEntry>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MetricTimelineEntry>();

        foreach (var entry in _queue)
        {
            if (entry.PeriodEnd >= fromUtc && entry.PeriodStart <= toUtc)
            {
                if (metricName == null || string.Equals(entry.MetricName, metricName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<MetricTimelineEntry>>(result);
    }

    public ValueTask<IReadOnlyList<IMetricSnapshot>> GetAggregatedSnapshotsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var matchingDeltas = new List<IMetricSnapshot>();

        foreach (var entry in _queue)
        {
            if (entry.PeriodEnd >= fromUtc && entry.PeriodStart <= toUtc)
            {
                if (metricName == null || string.Equals(entry.MetricName, metricName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingDeltas.Add(entry.Delta);
                }
            }
        }

        var aggregated = matchingDeltas.AggregateSnapshots();
        return ValueTask.FromResult(aggregated);
    }

    private void TrimExcess()
    {
        if (_queue.Count <= _maxCapacity && _retention == TimeSpan.MaxValue)
        {
            return;
        }

        if (!Monitor.TryEnter(_trimLock))
        {
            return; // Trimming is already in progress on another thread
        }

        try
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(_retention);

            while (_queue.TryPeek(out var oldest))
            {
                if (oldest.PeriodEnd < cutoff || _queue.Count > _maxCapacity)
                {
                    _queue.TryDequeue(out _);
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            Monitor.Exit(_trimLock);
        }
    }

    /// <summary>
    /// Clears all entries from the in-memory buffer.
    /// </summary>
    public void Clear()
    {
        lock (_trimLock)
        {
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
