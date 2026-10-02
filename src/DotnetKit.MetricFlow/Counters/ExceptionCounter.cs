using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

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
        metricState.Record(context.Exception);
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
        private long _totalExceptions;
        private readonly ConcurrentDictionary<string, long> _exceptionsByType = new();

        public string MetricName => metricName;
        public long TotalOperations => Interlocked.Read(ref _totalOperations);
        public long TotalExceptions => Interlocked.Read(ref _totalExceptions);
        public long TotalFailures => TotalExceptions;
        public IReadOnlyDictionary<string, long> ExceptionsByType => new Dictionary<string, long>(_exceptionsByType);

        public void Record(Exception? exception)
        {
            Interlocked.Increment(ref _totalOperations);

            if (exception != null)
            {
                Interlocked.Increment(ref _totalExceptions);

                var exType = exception.GetType().Name;
                _exceptionsByType.AddOrUpdate(exType, 1, (_, count) => count + 1);
            }
        }

        public ExceptionSnapshot ToSnapshot(string counterName)
        {
            return new ExceptionSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                TotalOperations: TotalOperations,
                TotalExceptions: TotalExceptions,
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
    long TotalExceptions,
    IReadOnlyDictionary<string, long> ExceptionsByType,
    DateTime Timestamp) : IMetricSnapshot
{
    public long TotalFailures => TotalExceptions;
    public double ExceptionRate => TotalOperations > 0 ? (double)TotalExceptions / TotalOperations : 0.0;
    public double FailureRate => ExceptionRate;

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Exceptions / Operations: {TotalExceptions} / {TotalOperations} ({ExceptionRate:P2})");
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
