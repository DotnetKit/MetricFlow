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
}
