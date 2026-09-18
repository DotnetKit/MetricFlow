using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow;

public class MetricTracker : MetricTrackerBase
{
    public DurationCounter DurationCounter { get; }

    public MetricTracker(
        string topic,
        Dictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0,
        ICounterConfigObservable? configObservable = null,
        IEnumerable<ICounter>? additionalCounters = null)
        : base(topic, topicTags, samplingRate, configObservable)
    {
        DurationCounter = new DurationCounter();
        RegisterCounter(DurationCounter);

        if (additionalCounters != null)
        {
            foreach (var counter in additionalCounters)
            {
                RegisterCounter(counter);
            }
        }
    }

    public DurationSnapshot? GetValues(string metricName)
    {
        return DurationCounter.GetSnapshot(metricName) as DurationSnapshot;
    }

    public MetricTracker AddExceptionCounter(string name = ExceptionCounter.DefaultCounterName)
    {
        RegisterCounter(new ExceptionCounter(name));
        return this;
    }

    public MetricTracker AddMemoryCounter(string name = MemoryCounter.DefaultCounterName)
    {
        RegisterCounter(new MemoryCounter(name));
        return this;
    }

    public MetricTracker AddThroughputCounter(string name = ThroughputCounter.DefaultCounterName)
    {
        RegisterCounter(new ThroughputCounter(name));
        return this;
    }

    public MetricTracker AddItemCounter(string name = ItemCounter.DefaultCounterName)
    {
        RegisterCounter(new ItemCounter(name));
        return this;
    }

    public ThroughputSnapshot? GetThroughputValues(string metricName, string counterName = ThroughputCounter.DefaultCounterName)
    {
        return GetSnapshot(metricName, counterName) as ThroughputSnapshot;
    }
}