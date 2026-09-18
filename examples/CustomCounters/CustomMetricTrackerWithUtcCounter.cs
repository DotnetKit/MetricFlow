using DotnetKit.MetricFlow.Abstractions;

namespace CustomCounters;

/// <summary>
/// Example of a custom metric tracker implementation with default UtcDurationCounter
/// </summary>
public class CustomMetricTrackerWithUtcCounter : MetricTrackerBase
{
    public CustomMetricTrackerWithUtcCounter(string topic, IReadOnlyDictionary<string, string>? topicTags = null, double? samplingRate = 1.0)
        : base(topic, topicTags, samplingRate)
    {
        RegisterCounter(new UtcDurationCounter());
    }
}