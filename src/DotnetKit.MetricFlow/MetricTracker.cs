using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Meters;
using DotnetKit.MetricFlow.Sinks;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Default in-memory metric tracker implementation that manages registered counters, sinks, and instrumentation bridges.
/// </summary>
public class MetricTracker : MetricTrackerBase
{
    /// <summary>
    /// Gets the default <see cref="Counters.DurationCounter"/> registered on this tracker.
    /// </summary>
    public DurationCounter DurationCounter { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTracker"/>.
    /// </summary>
    public MetricTracker(
        string topic,
        Dictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0,
        ICounterConfigObservable? configObservable = null,
        IEnumerable<ICounter>? additionalCounters = null)
        : this(topic, topicTags, samplingRate, configObservable, additionalCounters, meterBridge: null, sinks: null, sinkTriggers: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTracker"/> with a meter bridge.
    /// </summary>
    public MetricTracker(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IEnumerable<ICounter>? additionalCounters,
        IMetricMeterBridge? meterBridge)
        : this(topic, topicTags, samplingRate, configObservable, additionalCounters, meterBridge, sinks: null, sinkTriggers: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTracker"/> with full configuration parameters.
    /// </summary>
    public MetricTracker(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IEnumerable<ICounter>? additionalCounters,
        IMetricMeterBridge? meterBridge,
        IEnumerable<IMetricSink>? sinks,
        MetricSinkTriggerOptions? sinkTriggers,
        DurationCounter? durationCounter = null)
        : base(topic, topicTags, samplingRate, configObservable, meterBridge, sinks, sinkTriggers)
    {
        DurationCounter = durationCounter ?? new DurationCounter();
        RegisterCounter(DurationCounter);

        if (additionalCounters != null)
        {
            foreach (var counter in additionalCounters)
            {
                RegisterCounter(counter);
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTracker"/> from <see cref="MetricFlowOptions"/>.
    /// </summary>
    public MetricTracker(MetricFlowOptions options, IMetricMeterBridge? meterBridge = null)
        : this(
            topic: (options ?? throw new ArgumentNullException(nameof(options))).Topic,
            topicTags: options.TopicTags,
            samplingRate: options.SamplingRate,
            configObservable: options.ConfigObservable,
            additionalCounters: options.Counters,
            meterBridge: meterBridge ?? new MetricFlowMeterBridge(options.Topic, options.TopicTags, options.MeterOptions),
            sinks: options.Sinks,
            sinkTriggers: options.SinkTriggers,
            durationCounter: new DurationCounter(DurationCounter.DefaultCounterName, options.DurationOptions))
    {
        if (options.AutoAddExceptionCounter)
        {
            this.AddExceptionCounter();
        }
    }

    /// <summary>
    /// Gets the current <see cref="DurationSnapshot"/> for the specified metric name.
    /// </summary>
    /// <param name="metricName">The name of the tracked metric.</param>
    /// <returns>The duration snapshot, or null if no durations have been recorded.</returns>
    public DurationSnapshot? GetDurationSnapshot(string metricName)
    {
        return this.GetSnapshot<DurationSnapshot>(metricName, DurationCounter.Name);
    }
}