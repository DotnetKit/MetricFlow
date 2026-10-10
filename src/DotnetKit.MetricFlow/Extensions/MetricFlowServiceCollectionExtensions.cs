using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Meters;
using DotnetKit.MetricFlow.Sinks;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for configuring MetricFlow in dependency injection.
/// </summary>
public static class MetricFlowServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core MetricFlow engine and top-level facade (<see cref="IMetricFlow"/>) in the dependency injection container.
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent configuration of topic trackers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for default options.</param>
    /// <returns>The <see cref="IMetricFlowBuilder"/> instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricFlow(
        this IServiceCollection services,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MetricFlowOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(options.MeterOptions);

        // Register meter registry for BCL instrumentation
        services.TryAddSingleton<MetricFlowMeterRegistry>(sp =>
        {
            var defaultOptions = sp.GetService<MetricFlowOptions>() ?? options;
            var meterOptions = sp.GetService<MetricFlowMeterOptions>() ?? defaultOptions.MeterOptions;
            return new MetricFlowMeterRegistry(meterOptions);
        });

        // Register central registry & top-level facade
        services.TryAddSingleton<MetricFlowRegistry>(sp =>
        {
            var defaultOptions = sp.GetService<MetricFlowOptions>() ?? options;
            var meterRegistry = sp.GetRequiredService<MetricFlowMeterRegistry>();

            var registry = new MetricFlowRegistry(
                defaultTopic: defaultOptions.Topic,
                initialTrackers: null,
                trackerFactory: topic =>
                {
                    var keyed = sp.GetKeyedService<IMetricTracker>(topic);
                    if (keyed != null)
                    {
                        return keyed;
                    }

                    var meterBridge = meterRegistry.GetOrCreateBridge(topic, defaultOptions.TopicTags);
                    var diSinks = sp.GetServices<IMetricSink>();
                    var allSinks = defaultOptions.Sinks.Concat(diSinks).Distinct().ToList();

                    var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                    if (loggerFactory != null)
                    {
                        foreach (var sink in allSinks)
                        {
                            if (sink is DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSink loggerSink && loggerSink.NeedsLogger)
                            {
                                loggerSink.SetLogger(loggerFactory.CreateLogger(loggerSink.Options.CategoryName));
                            }
                        }
                    }

                    var tracker = new MetricTracker(
                        topic: topic,
                        topicTags: defaultOptions.TopicTags,
                        samplingRate: defaultOptions.SamplingRate,
                        configObservable: defaultOptions.ConfigObservable,
                        additionalCounters: defaultOptions.Counters,
                        meterBridge: meterBridge,
                        sinks: allSinks,
                        sinkTriggers: defaultOptions.SinkTriggers,
                        durationCounter: new DurationCounter(DurationCounter.DefaultCounterName, defaultOptions.DurationOptions));

                    if (defaultOptions.AutoAddExceptionCounter)
                    {
                        tracker.AddExceptionCounter();
                    }

                    return tracker;
                });


            // Register all statically configured topic trackers into the registry
            var registrations = sp.GetServices<MetricTrackerRegistration>();
            bool isFirst = true;
            foreach (var reg in registrations)
            {
                var tracker = sp.GetKeyedService<IMetricTracker>(reg.Topic);
                if (tracker != null)
                {
                    registry.RegisterTracker(tracker, setAsDefault: isFirst);
                    isFirst = false;
                }
            }

            return registry;
        });

        services.TryAddSingleton<IMetricFlow>(sp => sp.GetRequiredService<MetricFlowRegistry>());

        // Default un-keyed services resolve to IMetricFlow.DefaultTracker
        services.TryAddSingleton<IMetricTracker>(sp =>
        {
            var flow = sp.GetRequiredService<IMetricFlow>();
            return flow.DefaultTracker ?? flow.GetTracker(options.Topic);
        });

        services.TryAddSingleton<MetricTracker>(sp =>
            (sp.GetRequiredService<IMetricTracker>() as MetricTracker)
            ?? throw new InvalidOperationException("The default IMetricTracker is not a MetricTracker instance."));

        services.TryAddSingleton<IMetricSnapshotsSource>(sp =>
            (sp.GetRequiredService<IMetricTracker>() as IMetricSnapshotsSource)
            ?? throw new InvalidOperationException("The default IMetricTracker does not implement IMetricSnapshotsSource."));

        return new MetricFlowBuilder(services);
    }

    /// <summary>
    /// Registers a metric tracker for a specific topic, registering it as both a keyed service and in the <see cref="IMetricFlow"/> registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricFlowTracker(
        this IServiceCollection services,
        string topic,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var trackerOptions = new MetricFlowOptions { Topic = topic };
        configure?.Invoke(trackerOptions);

        // Ensure root MetricFlow facade is registered, forwarding initial options if first registration
        services.AddMetricFlow(opt =>
        {
            opt.Topic = trackerOptions.Topic;
            opt.TopicTags = trackerOptions.TopicTags;
            opt.SamplingRate = trackerOptions.SamplingRate;
            opt.ConfigObservable = trackerOptions.ConfigObservable;
            opt.AutoAddExceptionCounter = trackerOptions.AutoAddExceptionCounter;
            opt.MeterOptions = trackerOptions.MeterOptions;
            opt.SinkTriggers = trackerOptions.SinkTriggers;
            opt.DurationOptions = trackerOptions.DurationOptions;
            foreach (var sink in trackerOptions.Sinks)
            {
                opt.Sinks.Add(sink);
            }
            foreach (var counter in trackerOptions.Counters)
            {
                opt.Counters.Add(counter);
            }
        });

        // Register topic discovery metadata
        services.AddSingleton(new MetricTrackerRegistration(topic));

        // Keyed registrations
        services.AddKeyedSingleton<MetricTracker>(topic, (sp, key) =>
        {
            var meterRegistry = sp.GetRequiredService<MetricFlowMeterRegistry>();
            var meterBridge = meterRegistry.GetOrCreateBridge(trackerOptions.Topic, trackerOptions.TopicTags);
            var diSinks = sp.GetServices<IMetricSink>();
            var allSinks = trackerOptions.Sinks.Concat(diSinks).Distinct().ToList();

            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            if (loggerFactory != null)
            {
                foreach (var sink in allSinks)
                {
                    if (sink is DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSink loggerSink && loggerSink.NeedsLogger)
                    {
                        loggerSink.SetLogger(loggerFactory.CreateLogger(loggerSink.Options.CategoryName));
                    }
                }
            }

            var tracker = new MetricTracker(
                topic: trackerOptions.Topic,
                topicTags: trackerOptions.TopicTags,
                samplingRate: trackerOptions.SamplingRate,
                configObservable: trackerOptions.ConfigObservable,
                additionalCounters: trackerOptions.Counters,
                meterBridge: meterBridge,
                sinks: allSinks,
                sinkTriggers: trackerOptions.SinkTriggers,
                durationCounter: new DurationCounter(DurationCounter.DefaultCounterName, trackerOptions.DurationOptions));

            if (trackerOptions.AutoAddExceptionCounter)
            {
                tracker.AddExceptionCounter();
            }

            return tracker;
        });


        services.AddKeyedSingleton<IMetricTracker>(topic, (sp, key) => sp.GetRequiredKeyedService<MetricTracker>(key));
        services.AddKeyedSingleton<IMetricSnapshotsSource>(topic, (sp, key) => sp.GetRequiredKeyedService<MetricTracker>(key));

        return services;
    }

    /// <summary>
    /// Registers a default metric tracker with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricFlowTracker(
        this IServiceCollection services,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MetricFlowOptions();
        configure?.Invoke(options);

        return services.AddMetricFlowTracker(options.Topic, configure);
    }

    /// <summary>
    /// Registers MetricFlow with a specific topic name and optional configuration.
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent chaining.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricFlow(
        this IServiceCollection services,
        string topic,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        services.AddMetricFlowTracker(topic, configure);
        return new MetricFlowBuilder(services);
    }

    /// <summary>
    /// Registers a metric tracker for a specific topic (alias for <see cref="AddMetricFlowTracker(IServiceCollection, string, Action{MetricFlowOptions}?)"/>).
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent chaining.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricTracker(
        this IServiceCollection services,
        string topic,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        services.AddMetricFlowTracker(topic, configure);
        return new MetricFlowBuilder(services);
    }

    /// <summary>
    /// Registers a default metric tracker with optional configuration.
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent chaining.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricTracker(
        this IServiceCollection services,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetricFlowTracker(configure);
        return new MetricFlowBuilder(services);
    }
}
