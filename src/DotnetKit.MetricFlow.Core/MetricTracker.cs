using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{

    public class MetricTracker(string topic, Dictionary<string, string>? topicTags = null, int? samplingRate = 100) :
     MetricTrackerBase<StopWatchCounter>(topic, (name, metricMetadata) => new StopWatchCounter(name, metricMetadata), topicTags, samplingRate)
    {

    }
}