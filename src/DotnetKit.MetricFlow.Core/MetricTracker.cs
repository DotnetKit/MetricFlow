using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{

    public class MetricTracker(string topic, Dictionary<string, string>? topicTags = null, double? samplingRate = 1.0) :
     MetricTrackerBase<StopWatchCounter>(topic, (name, metricMetadata) => new StopWatchCounter(name, metricMetadata), topicTags, samplingRate)
    {

    }
}