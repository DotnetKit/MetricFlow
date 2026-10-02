using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Counter that tracks logical failures and exception-driven operation failures.
/// </summary>
public class FailureCounter(string name = FailureCounter.DefaultCounterName) : ICounter
{
    public const string DefaultCounterName = "Failure";

    private readonly ConcurrentDictionary<string, MetricFailureState> _states = new();

    public string Name { get; } = name;
    public bool IsEnabled { get; set; } = true;

    public object? OnIn(in InContext context) => null;

    public void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        var metricState = _states.GetOrAdd(context.MetricName, static name => new MetricFailureState(name));
        metricState.Record(context.Failed, context.Exception != null);
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

    public MetricFailureState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    public class MetricFailureState(string metricName)
    {
        private long _totalOperations;
        private long _totalFailures;
        private long _logicalFailures;
        private long _exceptionFailures;

        public string MetricName => metricName;
        public long TotalOperations => Interlocked.Read(ref _totalOperations);
        public long TotalFailures => Interlocked.Read(ref _totalFailures);
        public long LogicalFailures => Interlocked.Read(ref _logicalFailures);
        public long ExceptionFailures => Interlocked.Read(ref _exceptionFailures);

        public void Record(bool failed, bool hasException)
        {
            Interlocked.Increment(ref _totalOperations);

            if (failed || hasException)
            {
                Interlocked.Increment(ref _totalFailures);

                if (hasException)
                {
                    Interlocked.Increment(ref _exceptionFailures);
                }
                else
                {
                    Interlocked.Increment(ref _logicalFailures);
                }
            }
        }

        public FailureSnapshot ToSnapshot(string counterName)
        {
            return new FailureSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                TotalOperations: TotalOperations,
                TotalFailures: TotalFailures,
                LogicalFailures: LogicalFailures,
                ExceptionFailures: ExceptionFailures,
                Timestamp: DateTime.UtcNow
            );
        }
    }
}

/// <summary>
/// Represents a point-in-time snapshot of operation failure telemetry, distinguishing logical failures from exception failures.
/// </summary>
public record FailureSnapshot(
    string MetricName,
    string CounterName,
    long TotalOperations,
    long TotalFailures,
    long LogicalFailures,
    long ExceptionFailures,
    DateTime Timestamp) : IMetricSnapshot
{
    public double FailureRate => TotalOperations > 0 ? (double)TotalFailures / TotalOperations : 0.0;
    public double LogicalFailureRate => TotalOperations > 0 ? (double)LogicalFailures / TotalOperations : 0.0;
    public double ExceptionFailureRate => TotalOperations > 0 ? (double)ExceptionFailures / TotalOperations : 0.0;

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Failures / Operations: {TotalFailures} / {TotalOperations} ({FailureRate:P2})");
        if (LogicalFailures > 0 || ExceptionFailures > 0)
        {
            sb.AppendLine($"  - Logical Failures   : {LogicalFailures} ({LogicalFailureRate:P2})");
            sb.AppendLine($"  - Exception Failures : {ExceptionFailures} ({ExceptionFailureRate:P2})");
        }
        return sb.ToString();
    }

    public override string ToString() => ToFormattedString();
}
