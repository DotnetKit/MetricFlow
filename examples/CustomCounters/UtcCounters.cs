using DotnetKit.MetricFlow.Abstractions;

namespace CustomCounters;

/// <summary>
/// UTC based MetricTracker implementation
/// </summary>
public class UtcCounters : MetricTrackerBase
{
    public UtcCounters(string topic, IReadOnlyDictionary<string, string>? topicTags = null, double? samplingRate = 1.0)
        : base(topic, topicTags, samplingRate)
    {
        RegisterCounter(new UtcCounter());
    }
}