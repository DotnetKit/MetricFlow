using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Meters;
using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.Logging;

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
    /// Adds a tag enrichment delegate to configure topic-level tags.
    /// Supports fluent chaining and multiple enrichers.
    /// </summary>
    /// <param name="enricher">The tag enricher action.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddTagsEnricher(Action<Dictionary<string, string>> enricher)
    {
        ArgumentNullException.ThrowIfNull(enricher);

        TopicTags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        enricher(TopicTags);

        return this;
    }

    /// <summary>
    /// Adds a tag enrichment delegate (alias for <see cref="AddTagsEnricher"/>).
    /// </summary>
    /// <param name="enricher">The tag enricher action.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddTagEnricher(Action<Dictionary<string, string>> enricher)
        => AddTagsEnricher(enricher);

    /// <summary>
    /// Configuration options for System.Diagnostics.Metrics instrumentation.
    /// </summary>
    public MetricFlowMeterOptions MeterOptions { get; set; } = new();

    /// <summary>
    /// Configures the System.Diagnostics.Metrics meter options.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions ConfigureMeters(Action<MetricFlowMeterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(MeterOptions);
        return this;
    }

    /// <summary>
    /// Enables or disables System.Diagnostics.Metrics meter emission.
    /// </summary>
    /// <param name="enabled">Whether meter emission is enabled.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions EnableMeters(bool enabled = true)
    {
        MeterOptions.Enabled = enabled;
        return this;
    }

    /// <summary>
    /// Registered metric sinks that receive snapshots when triggers fire or upon manual flush.
    /// </summary>
    public IList<IMetricSink> Sinks { get; } = new List<IMetricSink>();

    /// <summary>
    /// Configuration options for execution-lifecycle sampling triggers (e.g. stride, failures, latency thresholds).
    /// </summary>
    public MetricSinkTriggerOptions SinkTriggers { get; set; } = new();

    /// <summary>
    /// Registers a custom metric sink to receive snapshot emissions.
    /// </summary>
    /// <param name="sink">The metric sink instance.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddSink(IMetricSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        Sinks.Add(sink);
        return this;
    }

    /// <summary>
    /// Registers a <see cref="ConsoleMetricSink"/> for structured console logging of metric snapshots.
    /// </summary>
    /// <param name="configure">Optional configuration for console output formatting.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddConsoleSink(Action<ConsoleMetricSinkOptions>? configure = null)
    {
        var options = new ConsoleMetricSinkOptions();
        configure?.Invoke(options);
        Sinks.Add(new ConsoleMetricSink(options));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> for structured ILogger logging of metric snapshots.
    /// </summary>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddLoggerSink(Action<LoggerMetricSinkOptions>? configure = null)
    {
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        Sinks.Add(new LoggerMetricSink(options));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> for structured ILogger logging of metric snapshots using a specific <see cref="ILogger"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddLoggerSink(ILogger logger, Action<LoggerMetricSinkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        Sinks.Add(new LoggerMetricSink(logger, options));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> for structured ILogger logging of metric snapshots using an <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddLoggerSink(ILoggerFactory loggerFactory, Action<LoggerMetricSinkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        Sinks.Add(new LoggerMetricSink(loggerFactory, options));
        return this;
    }

    /// <summary>
    /// Registers an <see cref="ObservableMetricSink"/> for reactive streaming of metric snapshots via <see cref="IObservable{T}"/>.
    /// </summary>
    /// <param name="observable">Outputs the created observable sink for subscribing.</param>
    /// <param name="name">Optional sink name.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions AddObservableSink(out ObservableMetricSink observable, string name = "Observable")
    {
        observable = new ObservableMetricSink(name);
        Sinks.Add(observable);
        return this;
    }

    /// <summary>
    /// Configures execution-lifecycle sampling triggers for emitting snapshots to sinks without a timer.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public MetricFlowOptions ConfigureSinkSampling(Action<MetricSinkTriggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(SinkTriggers);
        return this;
    }

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
    /// Configures whether to automatically register an exception counter on the tracker.
    /// </summary>
    /// <param name="enabled">Whether to automatically register an exception counter. Defaults to true.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddExceptionCounter(bool enabled = true)
    {
        AutoAddExceptionCounter = enabled;
        return this;
    }


    /// <summary>
    /// Additional counters to register with the tracker.
    /// </summary>
    public IList<ICounter> Counters { get; } = new List<ICounter>();

    /// <summary>
    /// Adds a <see cref="ThroughputCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">Optional custom counter name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddThroughputCounter(string name = ThroughputCounter.DefaultCounterName)
    {
        Counters.Add(new ThroughputCounter(name));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="MemoryCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">Optional custom counter name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddMemoryCounter(string name = MemoryCounter.DefaultCounterName)
    {
        Counters.Add(new MemoryCounter(name));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="FailureCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="FailureCounter.DefaultCounterName"/>.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddFailureCounter(string name = FailureCounter.DefaultCounterName)
    {
        Counters.Add(new FailureCounter(name));
        return this;
    }

    /// <summary>
    /// Adds a <see cref="HierarchyCounter"/> to track hierarchical parent-child operations across async execution contexts.
    /// </summary>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="HierarchyCounter.DefaultCounterName"/>.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddHierarchyCounter(string name = HierarchyCounter.DefaultCounterName)
    {
        Counters.Add(new HierarchyCounter(name));
        return this;
    }

    /// <summary>
    /// Enables hierarchical parent-child scope tracking (alias for <see cref="AddHierarchyCounter"/>).
    /// </summary>
    /// <param name="enabled">Whether to add the hierarchy counter.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions EnableHierarchical(bool enabled = true)
    {
        if (enabled && !Counters.Any(c => c is HierarchyCounter))
        {
            AddHierarchyCounter();
        }
        return this;
    }

    /// <summary>
    /// Adds a <see cref="DimensionCounter"/> to the configured counters.
    /// </summary>
    /// <param name="dimensionKey">The target tag or metadata key to aggregate on.</param>
    /// <param name="name">Optional custom counter name.</param>
    /// <param name="maxUniqueValues">Maximum unique values before overflow bucket.</param>
    /// <param name="overflowBucket">Overflow bucket name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddDimensionCounter(
        string dimensionKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
    {
        Counters.Add(new DimensionCounter(dimensionKey, name, maxUniqueValues, overflowBucket));
        return this;
    }

    /// <summary>
    /// Adds a composite multi-tag <see cref="DimensionCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="dimensionKeys">The list of tag keys to combine.</param>
    /// <param name="delimiter">Delimiter used to join tag values.</param>
    /// <param name="maxUniqueValues">Maximum unique combinations before overflow.</param>
    /// <param name="overflowBucket">Overflow bucket name.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddDimensionCounter(
        string name,
        IEnumerable<string> dimensionKeys,
        string delimiter = " / ",
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
    {
        Counters.Add(new DimensionCounter(name, dimensionKeys, delimiter, maxUniqueValues, overflowBucket));
        return this;
    }

    /// <summary>
    /// Adds a computed lambda <see cref="DimensionCounter"/> to the configured counters.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="selector">Function computing the dimension key from tags and metadata.</param>
    /// <param name="maxUniqueValues">Maximum unique values before overflow.</param>
    /// <param name="overflowBucket">Overflow bucket name.</param>
    /// <param name="dimensionName">Optional dimension label.</param>
    /// <returns>The options instance for chaining.</returns>
    public MetricFlowOptions AddDimensionCounter(
        string name,
        Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> selector,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]",
        string? dimensionName = null)
    {
        Counters.Add(new DimensionCounter(name, selector, maxUniqueValues, overflowBucket, dimensionName));
        return this;
    }
}

