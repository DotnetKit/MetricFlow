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
    /// Registers a <see cref="DimensionCounter"/> on the metric tracker to aggregate operation counts by a dimension tag or metadata key.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="dimensionKey">The target tag or metadata key to aggregate on (e.g. "country", "status").</param>
    /// <param name="name">Optional custom counter name. Defaults to "Dimension:{dimensionKey}".</param>
    /// <param name="maxUniqueValues">Maximum number of unique dimension values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddDimensionCounter<T>(
        this T tracker,
        string dimensionKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new DimensionCounter(dimensionKey, name, maxUniqueValues, overflowBucket));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="DimensionCounter"/> targeting a composite multi-tag dimension.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">The counter name.</param>
    /// <param name="dimensionKeys">The list of tag keys to combine (e.g. ["country", "payment_method"]).</param>
    /// <param name="delimiter">Delimiter used to join tag values. Defaults to " / ".</param>
    /// <param name="maxUniqueValues">Maximum unique combinations before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct combinations exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddDimensionCounter<T>(
        this T tracker,
        string name,
        IEnumerable<string> dimensionKeys,
        string delimiter = " / ",
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new DimensionCounter(name, dimensionKeys, delimiter, maxUniqueValues, overflowBucket));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="DimensionCounter"/> with a custom computed key selector lambda.
    /// Enables conditional business counters, classification rules, or dynamic multi-attribute aggregations.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">The counter name.</param>
    /// <param name="selector">Function computing the dimension key from tags and metadata. Return null to skip or mark untagged.</param>
    /// <param name="maxUniqueValues">Maximum unique values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    /// <param name="dimensionName">Optional descriptive dimension label. Defaults to <paramref name="name"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddDimensionCounter<T>(
        this T tracker,
        string name,
        Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> selector,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]",
        string? dimensionName = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new DimensionCounter(name, selector, maxUniqueValues, overflowBucket, dimensionName));
        return tracker;
    }

    /// <summary>
    /// Alias for <see cref="AddDimensionCounter{T}(T, string, string?, int, string)"/>.
    /// </summary>
    public static T AddTagBreakdownCounter<T>(
        this T tracker,
        string tagKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        where T : IMetricTracker
        => tracker.AddDimensionCounter(tagKey, name ?? $"TagBreakdown:{tagKey}", maxUniqueValues, overflowBucket);

    /// <summary>
    /// Alias for <see cref="AddDimensionCounter{T}(T, string, IEnumerable{string}, string, int, string)"/>.
    /// </summary>
    public static T AddTagBreakdownCounter<T>(
        this T tracker,
        string name,
        IEnumerable<string> tagKeys,
        string delimiter = " / ",
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        where T : IMetricTracker
        => tracker.AddDimensionCounter(name, tagKeys, delimiter, maxUniqueValues, overflowBucket);

    /// <summary>
    /// Alias for <see cref="AddDimensionCounter{T}(T, string, Func{IReadOnlyDictionary{string, string}?, IReadOnlyDictionary{string, long}?, string?}, int, string, string?)"/>.
    /// </summary>
    public static T AddComputedBreakdownCounter<T>(
        this T tracker,
        string name,
        Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> selector,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]",
        string? dimensionName = null)
        where T : IMetricTracker
        => tracker.AddDimensionCounter(name, selector, maxUniqueValues, overflowBucket, dimensionName);

    /// <summary>
    /// Retrieves the <see cref="DimensionSnapshot"/> for a specific metric name and dimension or counter name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="dimensionOrCounterName">The dimension or tag key that was tracked, or explicit counter name.</param>
    /// <param name="counterName">Optional explicit counter name override if different from <paramref name="dimensionOrCounterName"/>.</param>
    /// <returns>The dimension snapshot or null.</returns>
    public static DimensionSnapshot? GetDimensionValues(
        this IMetricTracker tracker,
        string metricName,
        string dimensionOrCounterName,
        string? counterName = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        if (!string.IsNullOrEmpty(counterName))
        {
            return tracker.GetSnapshot(metricName, counterName) as DimensionSnapshot;
        }

        return (tracker.GetSnapshot(metricName, $"Dimension:{dimensionOrCounterName}") ??
                tracker.GetSnapshot(metricName, dimensionOrCounterName) ??
                tracker.GetSnapshot(metricName, $"TagBreakdown:{dimensionOrCounterName}")) as DimensionSnapshot;
    }

    /// <summary>
    /// Alias for <see cref="GetDimensionValues(IMetricTracker, string, string, string?)"/>.
    /// </summary>
    public static DimensionSnapshot? GetTagBreakdownValues(
        this IMetricTracker tracker,
        string metricName,
        string tagKey,
        string? counterName = null)
        => tracker.GetDimensionValues(metricName, tagKey, counterName);

    /// <summary>
    /// Alias for <see cref="GetDimensionValues(IMetricTracker, string, string, string?)"/>.
    /// </summary>
    public static DimensionSnapshot? GetComputedBreakdownValues(
        this IMetricTracker tracker,
        string metricName,
        string counterName)
        => tracker.GetDimensionValues(metricName, counterName);
}
