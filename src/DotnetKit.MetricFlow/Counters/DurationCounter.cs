using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

public class DurationCounter : ICounter
{
    public const string DefaultCounterName = "Duration";

    private readonly ConcurrentDictionary<string, MetricDurationState> _states = new();
    private readonly DurationCounterOptions _options;

    public string Name { get; }
    public bool IsEnabled { get; set; } = true;
    public DurationCounterOptions Options => _options;

    public DurationCounter(string name = DefaultCounterName)
        : this(name, new DurationCounterOptions())
    {
    }

    public DurationCounter(DurationCounterOptions options)
        : this(DefaultCounterName, options)
    {
    }

    public DurationCounter(string name, DurationCounterOptions options)
    {
        Name = name;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public object? OnIn(in InContext context)
    {
        if (!IsEnabled) return null;

        var state = _states.GetOrAdd(context.MetricName, name => new MetricDurationState(name, _options));
        state.IncrementIn();
        return Stopwatch.GetTimestamp();
    }

    public void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        var metricState = _states.GetOrAdd(context.MetricName, name => new MetricDurationState(name, _options));

        TimeSpan elapsed;
        if (context.Duration.HasValue)
        {
            elapsed = context.Duration.Value;
        }
        else if (state is long startTimestamp && startTimestamp > 0)
        {
            elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        }
        else
        {
            elapsed = TimeSpan.Zero;
        }

        metricState.RecordOut(elapsed, context.Failed);
    }

    public IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _states.TryGetValue(metricName, out var state) ? state.ToSnapshot(Name) : null;
    }

    public IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var state in _states.Values)
        {
            yield return state.ToSnapshot(Name);
        }
    }

    public void Reset() => _states.Clear();

    public MetricDurationState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    public class MetricDurationState(string metricName, DurationCounterOptions? options = null)
    {
        private readonly DurationCounterOptions _options = options ?? new DurationCounterOptions();
        private readonly QuantileReservoir? _reservoir = (options?.EnablePercentiles ?? true)
            ? new QuantileReservoir(options?.ReservoirSize ?? DurationCounterOptions.DefaultReservoirSize)
            : null;

        private long _inCount;
        private long _outCount;
        private long _failedCount;
        private long _totalDurationTicks;
        private long _minDurationTicks;
        private long _maxDurationTicks;

        public string MetricName => metricName;
        public long InCount => Interlocked.Read(ref _inCount);
        public long OutCount => Interlocked.Read(ref _outCount);
        public long FailedCount => Interlocked.Read(ref _failedCount);
        public TimeSpan TotalDuration => TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks));
        public TimeSpan MinDuration => TimeSpan.FromTicks(Interlocked.Read(ref _minDurationTicks));
        public TimeSpan MaxDuration => TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks));
        public TimeSpan AverageDuration
        {
            get
            {
                var count = OutCount;
                return count > 0 ? TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks) / count) : TimeSpan.Zero;
            }
        }

        public void IncrementIn() => Interlocked.Increment(ref _inCount);

        public void RecordOut(TimeSpan elapsed, bool failed)
        {
            var ticks = elapsed.Ticks;
            if (ticks < 0) ticks = 0;

            Interlocked.Increment(ref _outCount);
            if (failed) Interlocked.Increment(ref _failedCount);

            Interlocked.Add(ref _totalDurationTicks, ticks);
            UpdateMax(ticks);
            UpdateMin(ticks);

            _reservoir?.Record(ticks);
        }

        private void UpdateMax(long ticks)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref _maxDurationTicks);
                if (ticks <= current) break;
            } while (Interlocked.CompareExchange(ref _maxDurationTicks, ticks, current) != current);
        }

        private void UpdateMin(long ticks)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref _minDurationTicks);
                if (current != 0 && ticks >= current) break;
            } while (Interlocked.CompareExchange(ref _minDurationTicks, ticks, current) != current);
        }

        public DurationSnapshot ToSnapshot(string counterName)
        {
            TimeSpan? p50 = null;
            TimeSpan? p90 = null;
            TimeSpan? p95 = null;
            TimeSpan? p99 = null;
            IReadOnlyDictionary<double, TimeSpan>? percentiles = null;

            if (_reservoir != null && OutCount > 0)
            {
                var calculated = _reservoir.GetPercentiles(_options.Percentiles);
                percentiles = calculated;

                if (calculated.TryGetValue(0.50, out var v50)) p50 = v50;
                if (calculated.TryGetValue(0.90, out var v90)) p90 = v90;
                if (calculated.TryGetValue(0.95, out var v95)) p95 = v95;
                if (calculated.TryGetValue(0.99, out var v99)) p99 = v99;
            }

            return new DurationSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                InCount: InCount,
                OutCount: OutCount,
                FailedCount: FailedCount,
                TotalDuration: TotalDuration,
                AverageDuration: AverageDuration,
                MinDuration: MinDuration,
                MaxDuration: MaxDuration,
                Timestamp: DateTime.UtcNow,
                P50Duration: p50,
                P90Duration: p90,
                P95Duration: p95,
                P99Duration: p99,
                Percentiles: percentiles
            );
        }
    }
}

public record DurationSnapshot(
    string MetricName,
    string CounterName,
    long InCount,
    long OutCount,
    long FailedCount,
    TimeSpan TotalDuration,
    TimeSpan AverageDuration,
    TimeSpan MinDuration,
    TimeSpan MaxDuration,
    DateTime Timestamp,
    TimeSpan? P50Duration = null,
    TimeSpan? P90Duration = null,
    TimeSpan? P95Duration = null,
    TimeSpan? P99Duration = null,
    IReadOnlyDictionary<double, TimeSpan>? Percentiles = null) : IMetricSnapshot
{
    /// <summary>
    /// Gets the 50th percentile (median) duration.
    /// </summary>
    public TimeSpan? P50 => P50Duration;

    /// <summary>
    /// Gets the 90th percentile duration.
    /// </summary>
    public TimeSpan? P90 => P90Duration;

    /// <summary>
    /// Gets the 95th percentile duration.
    /// </summary>
    public TimeSpan? P95 => P95Duration;

    /// <summary>
    /// Gets the 99th percentile duration.
    /// </summary>
    public TimeSpan? P99 => P99Duration;

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Duration (min, max, avg): {MinDuration.TotalMilliseconds:F2} ms / {MaxDuration.TotalMilliseconds:F2} ms / {AverageDuration.TotalMilliseconds:F2} ms");
        if (P50Duration.HasValue || P90Duration.HasValue || P95Duration.HasValue || P99Duration.HasValue)
        {
            sb.AppendLine($"Percentiles (p50, p90, p95, p99): {FormatNullableMs(P50Duration)} / {FormatNullableMs(P90Duration)} / {FormatNullableMs(P95Duration)} / {FormatNullableMs(P99Duration)}");
        }
        sb.AppendLine($"Total duration: {TotalDuration.TotalMilliseconds:F2} ms");
        return sb.ToString();
    }

    private static string FormatNullableMs(TimeSpan? ts) => ts.HasValue ? $"{ts.Value.TotalMilliseconds:F2} ms" : "n/a";

    public override string ToString() => ToFormattedString();
}
