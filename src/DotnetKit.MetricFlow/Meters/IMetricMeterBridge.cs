using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotnetKit.MetricFlow.Meters;

/// <summary>
/// Bridge interface connecting MetricFlow tracking scopes with System.Diagnostics.Metrics instruments.
/// </summary>
public interface IMetricMeterBridge : IDisposable
{
    /// <summary>
    /// Gets the topic associated with this meter.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Gets the underlying System.Diagnostics.Metrics.Meter instance.
    /// </summary>
    Meter Meter { get; }

    /// <summary>
    /// Gets whether meter emission is active.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the meter configuration options.
    /// </summary>
    MetricFlowMeterOptions Options { get; }

    /// <summary>
    /// Records the start of an in-flight operation and returns the active tag list for subsequent decrement.
    /// </summary>
    TagList? RecordOperationIn(string metricName, IReadOnlyDictionary<string, string>? tags, IReadOnlyDictionary<string, string>? metadata);

    /// <summary>
    /// Decrements the active in-flight operation using the tag list captured at start.
    /// </summary>
    void RecordOperationInFlightEnd(string metricName, in TagList inFlightTags);

    /// <summary>
    /// Records completed operation measurements (duration histogram, total executions, items throughput, exceptions).
    /// </summary>
    void RecordOperationOut(
        string metricName,
        TimeSpan duration,
        bool failed,
        Exception? exception,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, string>? metadata);

    /// <summary>
    /// Records a point-in-time functional metric increment to System.Diagnostics.Metrics Counter&lt;long&gt; instruments.
    /// </summary>
    void RecordMetric(
        string metricName,
        long value,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, string>? metadata)
    {
    }

    /// <summary>
    /// Resets cardinality caches and internal state.
    /// </summary>
    void Reset();
}
