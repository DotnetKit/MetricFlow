using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Configuration options for MetricFlow.
/// </summary>
public class MetricFlowOptions
{
    /// <summary>
    /// The topic name assigned to the MetricTracker. Defaults to "Application".
    /// </summary>
    public string Topic { get; set; } = "Application";

    /// <summary>
    /// Optional static tags attached at the Topic level (e.g. environment, service name).
    /// </summary>
    public Dictionary<string, string>? TopicTags { get; set; }

    /// <summary>
    /// Sampling rate between 0.0 and 1.0 (or null to track 100%). Defaults to 1.0.
    /// </summary>
    public double? SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Optional configuration observable for dynamic counter reconfiguration.
    /// </summary>
    public ICounterConfigObservable? ConfigObservable { get; set; }

    /// <summary>
    /// Whether to automatically register an exception counter on the tracker.
    /// Defaults to true.
    /// </summary>
    public bool AutoAddExceptionCounter { get; set; } = true;

    /// <summary>
    /// Additional counters to register with the tracker.
    /// </summary>
    public IList<ICounter> Counters { get; } = new List<ICounter>();

    /// <summary>
    /// Adds a <see cref="ThroughputCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">Optional custom counter name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddThroughputCounter(string name = ThroughputCounter.DefaultCounterName)
    {
        Counters.Add(new ThroughputCounter(name));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="MemoryCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">Optional custom counter name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddMemoryCounter(string name = MemoryCounter.DefaultCounterName)
    {
        Counters.Add(new MemoryCounter(name));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="TagBreakdownCounter"/> to the configured counters.
    /// </summary>
    /// <param name="tagKey">The target tag or metadata key to aggregate on.</param>
    /// <param name="name">Optional custom counter name.</param>
    /// <param name="maxUniqueValues">Maximum unique values before overflow bucket.</param>
    /// <param name="overflowBucket">Overflow bucket name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddTagBreakdownCounter(
        string tagKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
    {
        Counters.Add(new TagBreakdownCounter(tagKey, name, maxUniqueValues, overflowBucket));
        return this;
    }
}
