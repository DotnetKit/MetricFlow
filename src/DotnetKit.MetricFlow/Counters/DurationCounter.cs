using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters
{
    public class DurationCounter : ICounter
    {
        public const string DefaultCounterName = "Duration";

        private readonly ConcurrentDictionary<string, MetricDurationState> _states = new();

        public string Name { get; }
        public bool IsEnabled { get; set; } = true;

        public DurationCounter(string name = DefaultCounterName)
        {
            Name = name;
        }

        public object? OnIn(in InContext context)
        {
            if (!IsEnabled) return null;

            var state = _states.GetOrAdd(context.MetricName, static name => new MetricDurationState(name));
            state.IncrementIn();
            return Stopwatch.GetTimestamp();
        }

        public void OnOut(object? state, in OutContext context)
        {
            if (!IsEnabled) return;

            var metricState = _states.GetOrAdd(context.MetricName, static name => new MetricDurationState(name));

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

        public class MetricDurationState(string metricName)
        {
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
                    Timestamp: DateTime.UtcNow
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
        DateTime Timestamp) : IMetricSnapshot
    {


        public string ToFormattedString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
            sb.AppendLine($"Count (in, out, failed): {InCount} / {OutCount} / {FailedCount}");
            sb.AppendLine($"Avg duration: {AverageDuration.TotalMilliseconds:F2} ms");
            sb.AppendLine($"Duration (min, max): {MinDuration.TotalMilliseconds:F2} ms / {MaxDuration.TotalMilliseconds:F2} ms");
            sb.AppendLine($"Total duration: {TotalDuration.TotalMilliseconds:F2} ms");
            return sb.ToString();
        }

        public override string ToString() => ToFormattedString();
    }
}
