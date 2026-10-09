using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Hierarchy;

// ReSharper disable once CheckNamespace
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
    /// Registers a <see cref="FailureCounter"/> on the metric tracker to record logical and exception operation failures.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="FailureCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddFailureCounter<T>(this T tracker, string name = FailureCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new FailureCounter(name));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="HierarchyCounter"/> on the metric tracker to automatically correlate parent-child operations across async contexts.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="HierarchyCounter.DefaultCounterName"/>.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddHierarchyCounter<T>(this T tracker, string name = HierarchyCounter.DefaultCounterName)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.RegisterCounter(new HierarchyCounter(name));
        return tracker;
    }

    /// <summary>
    /// Retrieves a typed snapshot of type <typeparamref name="TSnapshot"/> for a specific metric name,
    /// optionally filtered by counter name.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of metric snapshot to return.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. When omitted or null, returns the first snapshot matching <typeparamref name="TSnapshot"/>.</param>
    /// <returns>The typed metric snapshot, or <c>null</c> if not found or not tracked.</returns>
    public static TSnapshot? GetSnapshot<TSnapshot>(
        this IMetricTracker tracker,
        string metricName,
        string? counterName = null) where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(tracker);

        if (!string.IsNullOrEmpty(counterName))
        {
            return tracker.GetSnapshot(metricName, counterName) as TSnapshot;
        }

        return tracker.GetSnapshots(metricName).OfType<TSnapshot>().FirstOrDefault();
    }

    /// <summary>
    /// Retrieves all snapshots of type <typeparamref name="TSnapshot"/> for a specific metric name.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of metric snapshot to filter by.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <returns>An enumerable collection of typed snapshots.</returns>
    public static IEnumerable<TSnapshot> GetSnapshots<TSnapshot>(
        this IMetricTracker tracker,
        string metricName) where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshots(metricName).OfType<TSnapshot>();
    }

    /// <summary>
    /// Retrieves all snapshots of type <typeparamref name="TSnapshot"/> collected by this source.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of metric snapshot to filter by.</typeparam>
    /// <param name="source">The metric snapshots source.</param>
    /// <returns>An enumerable collection of typed snapshots.</returns>
    public static IEnumerable<TSnapshot> GetAllSnapshots<TSnapshot>(
        this IMetricSnapshotsSource source) where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().OfType<TSnapshot>();
    }

    /// <summary>
    /// Retrieves the <see cref="DurationSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="DurationCounter.DefaultCounterName"/>.</param>
    /// <returns>The duration snapshot or null.</returns>
    public static DurationSnapshot? GetDurationSnapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = DurationCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<DurationSnapshot>(metricName, counterName);
    }

    /// <summary>
    /// Retrieves the <see cref="ThroughputSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="ThroughputCounter.DefaultCounterName"/>.</param>
    /// <returns>The throughput snapshot or null.</returns>
    public static ThroughputSnapshot? GetThroughputSnapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = ThroughputCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<ThroughputSnapshot>(metricName, counterName);
    }

    /// <summary>
    /// Retrieves the <see cref="ExceptionSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="ExceptionCounter.DefaultCounterName"/>.</param>
    /// <returns>The exception snapshot or null.</returns>
    public static ExceptionSnapshot? GetExceptionSnapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = ExceptionCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<ExceptionSnapshot>(metricName, counterName);
    }

    /// <summary>
    /// Retrieves the <see cref="MemorySnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="MemoryCounter.DefaultCounterName"/>.</param>
    /// <returns>The memory snapshot or null.</returns>
    public static MemorySnapshot? GetMemorySnapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = MemoryCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<MemorySnapshot>(metricName, counterName);
    }

    /// <summary>
    /// Retrieves the <see cref="FailureSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="FailureCounter.DefaultCounterName"/>.</param>
    /// <returns>The failure snapshot or null.</returns>
    public static FailureSnapshot? GetFailureShapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = FailureCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<FailureSnapshot>(metricName, counterName);
    }

    /// <summary>
    /// Retrieves the <see cref="HierarchyTreeSnapshot"/> for a specific metric name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">Optional counter name. Defaults to <see cref="HierarchyCounter.DefaultCounterName"/>.</param>
    /// <returns>The hierarchy tree snapshot or null.</returns>
    public static HierarchyTreeSnapshot? GetHierarchySnapshot(
        this IMetricTracker tracker,
        string metricName,
        string counterName = HierarchyCounter.DefaultCounterName)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.GetSnapshot<HierarchyTreeSnapshot>(metricName, counterName);
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
    /// Retrieves the <see cref="DimensionSnapshot"/> for a specific metric and dimension name, or <c>null</c> if not tracked.
    /// </summary>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="dimensionName">The dimension tag key (e.g. "region") or explicit counter name.</param>
    /// <returns>The dimension snapshot or null.</returns>
    public static DimensionSnapshot? GetDimensionSnapshot(
        this IMetricTracker tracker,
        string metricName,
        string dimensionName)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        return tracker.GetSnapshot<DimensionSnapshot>(metricName, $"Dimension:{dimensionName}") ??
               tracker.GetSnapshot<DimensionSnapshot>(metricName, dimensionName) ??
               tracker.GetSnapshot<DimensionSnapshot>(metricName, $"TagBreakdown:{dimensionName}");
    }
}
