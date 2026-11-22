using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Lightweight functional metric counter that tracks total event counts and accumulated values
/// without any duration, stopwatch, or memory telemetry overhead.
/// Responds to both point-in-time <see cref="OnRecord"/> calls and scoped <see cref="OnOut"/> completions.
/// </summary>
public class CountCounter(string name = CountCounter.DefaultCounterName) : CounterBase<object?>(name)
{
    /// <summary>
    /// The default counter name ("Count").
    /// </summary>
    public const string DefaultCounterName = "Count";

    private readonly ConcurrentDictionary<string, MetricCountState> _states = new(StringComparer.OrdinalIgnoreCase);
    private long _sequenceCounter;

    /// <inheritdoc />
    public override object? OnIn(in InContext context)
    {
        if (!IsEnabled) return null;

        _ = _states.GetOrAdd(
            context.MetricName,
            metricName => new MetricCountState(metricName, Interlocked.Increment(ref _sequenceCounter)));

        return null;
    }

    /// <inheritdoc />
    public override void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        var countState = _states.GetOrAdd(
            context.MetricName,
            metricName => new MetricCountState(metricName, Interlocked.Increment(ref _sequenceCounter)));

        var increment = 1L;
        if (ItemCountExtractor.TryExtractItemCount(context.Tags, context.Metadata, out var parsedItems))
        {
            increment = parsedItems;
        }

        countState.Add(increment);
    }

    /// <inheritdoc />
    public override void OnRecord(in RecordContext context)
    {
        if (!IsEnabled) return;

        var countState = _states.GetOrAdd(
            context.MetricName,
            metricName => new MetricCountState(metricName, Interlocked.Increment(ref _sequenceCounter)));

        countState.Add(context.Value);
    }

    /// <inheritdoc />
    public override IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _states.TryGetValue(metricName, out var state) ? state.ToSnapshot(Name) : null;
    }

    /// <inheritdoc />
    public override IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var state in _states.Values.OrderBy(static s => s.Sequence))
        {
            yield return state.ToSnapshot(Name);
        }
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _states.Clear();
        Interlocked.Exchange(ref _sequenceCounter, 0);
    }

    /// <summary>
    /// Gets the raw mutable state for the specified metric name, if tracked.
    /// </summary>
    public MetricCountState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    /// <summary>
    /// Thread-safe state tracking accumulated count and number of recorded events for a metric.
    /// </summary>
    public class MetricCountState(string metricName, long sequence = 0)
    {
        private long _totalCount;
        private long _eventCount;

        public string MetricName => metricName;
        public long Sequence => sequence;
        public long TotalCount => Interlocked.Read(ref _totalCount);
        public long EventCount => Interlocked.Read(ref _eventCount);

        public void Add(long value)
        {
            Interlocked.Add(ref _totalCount, value);
            Interlocked.Increment(ref _eventCount);
        }

        public CountSnapshot ToSnapshot(string counterName)
        {
            return new CountSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                TotalCount: TotalCount,
                EventCount: EventCount,
                Timestamp: DateTime.UtcNow);
        }
    }
}

/// <summary>
/// Immutable snapshot representing accumulated pure functional counts for a metric.
/// </summary>
public record CountSnapshot(
    string MetricName,
    string CounterName,
    long TotalCount,
    long EventCount,
    DateTime Timestamp) : IMetricSnapshot
{
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Count            : {TotalCount:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Events Recorded        : {EventCount:N0}");
        return sb.ToString();
    }

    public override string ToString() => ToFormattedString();
}
