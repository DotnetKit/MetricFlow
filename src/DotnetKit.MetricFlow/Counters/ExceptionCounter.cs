using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters
{
    public class ExceptionCounter : ICounter
    {
        public const string DefaultCounterName = "Exception";

        private readonly ConcurrentDictionary<string, MetricExceptionState> _states = new();

        public string Name { get; }
        public bool IsEnabled { get; set; } = true;

        public ExceptionCounter(string name = DefaultCounterName)
        {
            Name = name;
        }

        public object? OnIn(in InContext context) => null;

        public void OnOut(object? state, in OutContext context)
        {
            if (!IsEnabled) return;

            var metricState = _states.GetOrAdd(context.MetricName, static name => new MetricExceptionState(name));
            metricState.Record(context.Failed, context.Exception);
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

        public MetricExceptionState? GetState(string metricName)
        {
            _states.TryGetValue(metricName, out var state);
            return state;
        }

        public class MetricExceptionState(string metricName)
        {
            private long _totalOperations;
            private long _totalFailures;
            private readonly ConcurrentDictionary<string, long> _exceptionsByType = new();

            public string MetricName => metricName;
            public long TotalOperations => Interlocked.Read(ref _totalOperations);
            public long TotalFailures => Interlocked.Read(ref _totalFailures);
            public IReadOnlyDictionary<string, long> ExceptionsByType => new Dictionary<string, long>(_exceptionsByType);

            public void Record(bool failed, Exception? exception)
            {
                Interlocked.Increment(ref _totalOperations);

                if (failed || exception != null)
                {
                    Interlocked.Increment(ref _totalFailures);

                    var exType = exception?.GetType().Name ?? "UnspecifiedError";
                    _exceptionsByType.AddOrUpdate(exType, 1, (_, count) => count + 1);
                }
            }

            public ExceptionSnapshot ToSnapshot(string counterName)
            {
                return new ExceptionSnapshot(
                    MetricName: metricName,
                    CounterName: counterName,
                    TotalOperations: TotalOperations,
                    TotalFailures: TotalFailures,
                    ExceptionsByType: ExceptionsByType,
                    Timestamp: DateTime.UtcNow
                );
            }
        }
    }

    public record ExceptionSnapshot(
        string MetricName,
        string CounterName,
        long TotalOperations,
        long TotalFailures,
        IReadOnlyDictionary<string, long> ExceptionsByType,
        DateTime Timestamp) : IMetricSnapshot
    {

        public double FailureRate => TotalOperations > 0 ? (double)TotalFailures / TotalOperations : 0.0;

        public string ToFormattedString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
            sb.AppendLine($"Failures / Operations: {TotalFailures} / {TotalOperations} ({FailureRate:P2})");
            if (ExceptionsByType.Count > 0)
            {
                sb.AppendLine("Exceptions Breakdown:");
                foreach (var (exType, count) in ExceptionsByType)
                {
                    sb.AppendLine($"  - {exType}: {count}");
                }
            }
            return sb.ToString();
        }

        public override string ToString() => ToFormattedString();
    }
}
