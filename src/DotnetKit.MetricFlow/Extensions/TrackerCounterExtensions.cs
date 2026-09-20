using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for registering and querying built-in counters on <see cref="IMetricTracker"/>.
/// </summary>
public static class TrackerCounterExtensions
{
    /// <summary>
    /// Registers a <see cref="ThroughputCounter"/> on the metric tracker.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="ThroughputCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddThroughputCounter<T>(this T tracker, string name = ThroughputCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new ThroughputCounter(name));
        return tracker;
    }

    /// <summary>
    /// Registers an <see cref="ItemCounter"/> on the metric tracker.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="ItemCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddItemCounter<T>(this T tracker, string name = ItemCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new ItemCounter(name));
        return tracker;
    }

    /// <summary>
    /// Registers an <see cref="ExceptionCounter"/> on the metric tracker.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="ExceptionCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddExceptionCounter<T>(this T tracker, string name = ExceptionCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new ExceptionCounter(name));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="MemoryCounter"/> on the metric tracker.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="MemoryCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddMemoryCounter<T>(this T tracker, string name = MemoryCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new MemoryCounter(name));
        return tracker;
    }

    /// <summary>
    /// Retrieves the <see cref="ThroughputSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="ThroughputCounter.DefaultCounterName"/>.</param>
    /// <returns>The throughput snapshot or null.</returns>
    public static ThroughputSnapshot? GetThroughputValues(
        this IMetricTracker tracker,
        string metricName,
        string counterName = ThroughputCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot(metricName, counterName) as ThroughputSnapshot;
    }

    /// <summary>
    /// Registers a <see cref="TagBreakdownCounter"/> on the metric tracker to aggregate operation counts by tag.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="tagKey">The target tag or metadata key to aggregate on (e.g. "country", "status").</param>
    /// <param name="name">Optional custom counter name. Defaults to "TagBreakdown:{tagKey}".</param>
    /// <param name="maxUniqueValues">Maximum number of unique tag values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddTagBreakdownCounter<T>(
        this T tracker,
        string tagKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new TagBreakdownCounter(tagKey, name, maxUniqueValues, overflowBucket));
        return tracker;
    }

    /// <summary>
    /// Retrieves the <see cref="TagBreakdownSnapshot"/> for a specific metric name and counter, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="tagKey">The tag key that was tracked, used to derive the default counter name "TagBreakdown:{tagKey}".</param>
    /// <param name="counterName">Optional explicit counter name. If null, defaults to "TagBreakdown:{tagKey}".</param>
    /// <returns>The tag breakdown snapshot or null.</returns>
    public static TagBreakdownSnapshot? GetTagBreakdownValues(
        this IMetricTracker tracker,
        string metricName,
        string tagKey,
        string? counterName = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        var resolvedCounterName = counterName ?? $"TagBreakdown:{tagKey}";
        return tracker.GetSnapshot(metricName, resolvedCounterName) as TagBreakdownSnapshot;
    }
}
