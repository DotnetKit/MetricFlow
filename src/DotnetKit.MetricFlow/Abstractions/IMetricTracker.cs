using System.Runtime.CompilerServices;
using DotnetKit.MetricFlow.Meters;

namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Defines the core contract for tracking and observing application metrics across registered counters and sinks.
/// </summary>
public interface IMetricTracker : IMetricSnapshotsSource
{
    /// <summary>
    /// Gets the topic or subsystem name associated with this metric tracker.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Gets the default tags applied to all metrics tracked under this topic, if any.
    /// </summary>
    Dictionary<string, string>? TopicTags { get; }

    /// <summary>
    /// Gets the meter bridge connecting this tracker to System.Diagnostics.Metrics, if configured.
    /// </summary>
    IMetricMeterBridge? MeterBridge => null;

    /// <summary>
    /// Registers a metric counter with this tracker.
    /// </summary>
    /// <param name="counter">The counter instance to register.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    IMetricTracker RegisterCounter(ICounter counter);

    /// <summary>
    /// Unregisters a metric counter by its name.
    /// </summary>
    /// <param name="counterName">The name of the counter to unregister.</param>
    /// <returns><c>true</c> if the counter was found and removed; otherwise, <c>false</c>.</returns>
    bool UnregisterCounter(string counterName);

    /// <summary>
    /// Enables or disables a registered counter by name at runtime.
    /// </summary>
    /// <param name="counterName">The name of the counter.</param>
    /// <param name="enabled"><c>true</c> to enable the counter; <c>false</c> to disable it.</param>
    void SetCounterEnabled(string counterName, bool enabled);

    /// <summary>
    /// Gets all registered metric counters.
    /// </summary>
    /// <returns>An enumerable collection of registered counters.</returns>
    IEnumerable<ICounter> GetCounters();

    /// <summary>
    /// Begins tracking an operation scope using a disposable tracker.
    /// Disposing the returned object as the operation and records duration, throughput, and outcome.
    /// </summary>
    /// <param name="metricName">The name of the metric or operation being tracked.</param>
    /// <param name="tags">Optional key-value string tags associated with this execution.</param>
    /// <param name="metadata">Optional numeric metadata (e.g. byte counts, item counts) associated with this execution.</param>
    /// <returns>A disposable scope object that marks the exit of the tracked operation upon disposal.</returns>
    IDisposable Track(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null);

    /// <summary>
    /// Begins tracking an operation scope using the caller's member name as the metric name.
    /// Disposing the returned object automatically completes the operation.
    /// </summary>
    /// <param name="tags">Optional key-value string tags associated with this execution.</param>
    /// <param name="metadata">Optional numeric metadata associated with this execution.</param>
    /// <param name="metricName">The caller member name, automatically supplied by the compiler.</param>
    /// <returns>A disposable scope object that marks the exit of the tracked operation upon disposal.</returns>
    IDisposable Track(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "");

    /// <summary>
    /// Records the entry point of an operation across all enabled counters.
    /// </summary>
    /// <param name="metricName">The name of the metric or operation.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "In/Out represents the core lifecycle pairing in MetricFlow domain")]
    void In(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null);

    /// <summary>
    /// Records the entry point of an operation across all enabled counters using the caller member name.
    /// </summary>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="metricName">The caller member name, automatically supplied by the compiler.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "In/Out represents the core lifecycle pairing in MetricFlow domain")]
    void In(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "");


    /// <summary>
    /// Records the exit or completion of an operation across all enabled counters.
    /// </summary>
    /// <param name="metricName">The name of the metric or operation.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="failed"><c>true</c> if the operation failed logically; otherwise, <c>false</c>.</param>
    /// <param name="exception">Optional exception thrown during the operation, if any.</param>
    /// <param name="duration">Optional explicit elapsed duration of the operation.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    void Out(
        string metricName,
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null);

    /// <summary>
    /// Records the exit or completion of an operation across all enabled counters using the caller member name.
    /// </summary>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="failed"><c>true</c> if the operation failed logically; otherwise, <c>false</c>.</param>
    /// <param name="exception">Optional exception thrown during the operation, if any.</param>
    /// <param name="duration">Optional explicit elapsed duration of the operation.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="metricName">The caller member name, automatically supplied by the compiler.</param>
    void Out(
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null,
        [CallerMemberName] string metricName = "");

    /// <summary>
    /// Gets the latest metric snapshot for a specific metric name and counter name.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="counterName">The name of the counter (e.g. "Duration", "Throughput").</param>
    /// <returns>The snapshot instance, or <c>null</c> if no data exists for the given metric and counter.</returns>
    IMetricSnapshot? GetSnapshot(string metricName, string counterName);

    /// <summary>
    /// Gets all current metric snapshots across all counters for the specified metric name.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <returns>A collection of snapshots for the metric.</returns>
    IEnumerable<IMetricSnapshot> GetSnapshots(string metricName);

    /// <summary>
    /// Registers a metric sink to receive snapshot emissions from this tracker.
    /// </summary>
    /// <param name="sink">The sink instance.</param>
    /// <returns>This tracker for fluent chaining.</returns>
    IMetricTracker RegisterSink(Sinks.IMetricSink sink) => this;

    /// <summary>
    /// Unregisters a metric sink by name.
    /// </summary>
    /// <param name="sinkName">The name of the sink to unregister.</param>
    /// <returns>True if the sink was removed; otherwise, false.</returns>
    bool UnregisterSink(string sinkName) => false;

    /// <summary>
    /// Gets all registered metric sinks.
    /// </summary>
    /// <returns>An enumerable of registered sinks.</returns>
    IEnumerable<Sinks.IMetricSink> GetSinks() => [];

    /// <summary>
    /// Synchronously flushes all current snapshots to registered sinks.
    /// </summary>
    void FlushSinks() { }

    /// <summary>
    /// Asynchronously flushes all current snapshots to registered sinks.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A ValueTask representing the flush operation.</returns>
    ValueTask FlushSinksAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Clears all recorded metric states across all registered counters.
    /// </summary>
    void Clear();
}