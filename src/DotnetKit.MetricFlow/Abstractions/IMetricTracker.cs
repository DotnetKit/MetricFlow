using System.Runtime.CompilerServices;
using DotnetKit.MetricFlow.Meters;

namespace DotnetKit.MetricFlow.Abstractions;

public interface IMetricTracker : IMetricSnapshotsSource
{
    string Topic { get; }
    Dictionary<string, string>? TopicTags { get; }

    /// <summary>
    /// Gets the meter bridge connecting this tracker to System.Diagnostics.Metrics, if configured.
    /// </summary>
    IMetricMeterBridge? MeterBridge => null;

    IMetricTracker RegisterCounter(ICounter counter);
    bool UnregisterCounter(string counterName);
    void SetCounterEnabled(string counterName, bool enabled);
    IEnumerable<ICounter> GetCounters();

    IDisposable Track(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null);
    IDisposable Track(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "");

    void In(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null);
    void In(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "");

    void Out(
        string metricName,
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null);
    void Out(
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null,
        [CallerMemberName] string metricName = "");

    IMetricSnapshot? GetSnapshot(string metricName, string counterName);
    IEnumerable<IMetricSnapshot> GetSnapshots(string metricName);

    /// <summary>
    /// Registers a metric sink to receive snapshot emissions from this tracker.
    /// </summary>
    /// <param name="sink">The sink instance.</param>
    /// <returns>This tracker for fluent chaining.</returns>
    IMetricTracker RegisterSink(DotnetKit.MetricFlow.Sinks.IMetricSink sink) => this;

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
    IEnumerable<DotnetKit.MetricFlow.Sinks.IMetricSink> GetSinks() => [];

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

    void Clear();
}