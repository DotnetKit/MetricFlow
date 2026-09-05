using DotnetKit.MetricFlow.Tracker.Abstractions;

namespace CustomCounters
{
    /// <summary>
    ///  UTC based MetricTracker implementation
    /// </summary>
    public class UtcCounters(string topic, Dictionary<string, string>? topicTags = null, double? samplingRate = 1.0)
        : MetricTrackerBase<UtcCounter>(topic, (name, metadata) => new UtcCounter(name, metadata), topicTags, samplingRate)
    {
    }
}