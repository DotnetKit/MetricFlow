using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;

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
}
