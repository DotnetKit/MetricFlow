using DotnetKit.MetricFlow.Tracker.Abstractions;

namespace DotnetKit.MetricFlow.Tracker
{

    public class MetricTracker(string topic, Dictionary<string, string>? topicTags = null, double? samplingRate = 1.0) :
     MetricTrackerBase<StopWatchCounter>(topic, (name, metricMetadata) => new StopWatchCounter(name, metricMetadata), topicTags, samplingRate)
    {

    }
}