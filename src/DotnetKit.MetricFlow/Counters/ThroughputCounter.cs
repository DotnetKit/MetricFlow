using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Metric counter that tracks processed item count, batch operations,
/// and calculates processing throughput (items/second).
/// Tag keys recognized (case-insensitive): "items", "count", "batch_size".
/// </summary>
public class ThroughputCounter(string name = ThroughputCounter.DefaultCounterName) : CounterBase<long>(name)
{
    public const string DefaultCounterName = "Throughput";

    private readonly ConcurrentDictionary<string, MetricThroughputState> _states = new();

    public override long OnIn(in InContext context)
    {
        if (!IsEnabled) return 0;
        return Stopwatch.GetTimestamp();
    }

    public override void OnOut(long state, in OutContext context)
    {
        if (!IsEnabled) return;

        var throughputState = _states.GetOrAdd(context.MetricName, static name => new MetricThroughputState(name));

        long items = 1;
        if (context.Tags != null && TryExtractItemCount(context.Tags, out var parsedItems))
        {
            items = parsedItems;
        }

        TimeSpan duration = context.Duration ?? (state > 0 ? Stopwatch.GetElapsedTime(state) : TimeSpan.Zero);
        throughputState.Record(items, duration, context.Failed);
    }

    private static bool TryExtractItemCount(IReadOnlyDictionary<string, string> tags, out long items)
    {
        if (tags.TryGetValue("items", out var itemsStr) && long.TryParse(itemsStr, out items)) return true;
        if (tags.TryGetValue("count", out var countStr) && long.TryParse(countStr, out items)) return true;
        if (tags.TryGetValue("batch_size", out var batchStr) && long.TryParse(batchStr, out items)) return true;

        // Fallback: search case-insensitively if the dictionary does not use OrdinalIgnoreCase
        foreach (var (key, value) in tags)
        {
            if (string.Equals(key, "items", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "batch_size", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(value, out items)) return true;
            }
        }

        items = 1;
        return false;
    }

    public override IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _states.TryGetValue(metricName, out var state) ? state.ToSnapshot(Name) : null;
    }

    public override IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var state in _states.Values)
        {
            yield return state.ToSnapshot(Name);
        }
    }

    public override void Reset() => _states.Clear();

    public MetricThroughputState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    public class MetricThroughputState(string metricName)
    {
        private long _totalItems;
        private long _totalOperations;
        private long _failedOperations;
        private long _totalDurationTicks;

        public string MetricName => metricName;
        public long TotalItems => Interlocked.Read(ref _totalItems);
        public long TotalOperations => Interlocked.Read(ref _totalOperations);
        public long FailedOperations => Interlocked.Read(ref _failedOperations);
        public TimeSpan TotalDuration => TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks));

        public double ItemsPerSecond
        {
            get
            {
                var sec = TotalDuration.TotalSeconds;
                return sec > 0 ? TotalItems / sec : 0;
            }
        }

        public double AverageItemsPerOperation
        {
            get
            {
                var ops = TotalOperations;
                return ops > 0 ? (double)TotalItems / ops : 0;
            }
        }

        public void Record(long items, TimeSpan duration, bool failed = false)
        {
            Interlocked.Add(ref _totalItems, items);
            Interlocked.Increment(ref _totalOperations);
            if (failed) Interlocked.Increment(ref _failedOperations);
            Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        }

        public ThroughputSnapshot ToSnapshot(string counterName)
        {
            return new ThroughputSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                TotalItems: TotalItems,
                TotalOperations: TotalOperations,
                TotalDuration: TotalDuration,
                ItemsPerSecond: ItemsPerSecond,
                AverageItemsPerOperation: AverageItemsPerOperation,
                Timestamp: DateTime.UtcNow,
                FailedOperations: FailedOperations
            );
        }
    }
}

/// <summary>
/// Alias for <see cref="ThroughputCounter"/> with a default counter name of "Item".
/// </summary>
public class ItemCounter(string name = ItemCounter.DefaultCounterName) : ThroughputCounter(name)
{
    public new const string DefaultCounterName = "Item";
}

public record ThroughputSnapshot(
    string MetricName,
    string CounterName,
    long TotalItems,
    long TotalOperations,
    TimeSpan TotalDuration,
    double ItemsPerSecond,
    double AverageItemsPerOperation,
    DateTime Timestamp,
    long FailedOperations = 0) : IMetricSnapshot
{
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Total Items Processed : {TotalItems:N0}");
        sb.AppendLine($"Batch Operations       : {TotalOperations:N0} (avg {AverageItemsPerOperation:N1} items/op)");
        sb.AppendLine($"Throughput             : {ItemsPerSecond:N0} items/sec");
        return sb.ToString();
    }

    public override string ToString() => ToFormattedString();
}
