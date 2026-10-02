using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Meters;

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
        : this(topic, topicTags, samplingRate, configObservable, additionalCounters, meterBridge: null)
    {
    }

    public MetricTracker(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IEnumerable<ICounter>? additionalCounters,
        IMetricMeterBridge? meterBridge)
        : base(topic, topicTags, samplingRate, configObservable, meterBridge)
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

    public MetricTracker(MetricFlowOptions options, IMetricMeterBridge? meterBridge = null)
        : this(
            topic: (options ?? throw new ArgumentNullException(nameof(options))).Topic,
            topicTags: options.TopicTags,
            samplingRate: options.SamplingRate,
            configObservable: options.ConfigObservable,
            additionalCounters: options.Counters,
            meterBridge: meterBridge ?? new MetricFlowMeterBridge(options.Topic, options.TopicTags, options.MeterOptions))
    {
        if (options.AutoAddExceptionCounter)
        {
            this.AddExceptionCounter();
        }
    }

    public DurationSnapshot? GetValues(string metricName)
    {
        return this.GetSnapshot<DurationSnapshot>(metricName, DurationCounter.Name);
    }
}