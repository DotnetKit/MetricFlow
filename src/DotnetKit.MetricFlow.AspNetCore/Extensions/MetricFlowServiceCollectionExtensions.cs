using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Sinks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotnetKit.MetricFlow.AspNetCore.Extensions;

/// <summary>
/// Service collection extensions for configuring MetricFlow in ASP.NET Core.
/// </summary>
public static class MetricFlowServiceCollectionExtensions
{
    /// <summary>
    /// Registers MetricFlow and its ASP.NET Core services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricFlow(
        this IServiceCollection services,
        Action<MetricFlowAspNetCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MetricFlowAspNetCoreOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<MetricFlowOptions>(sp => sp.GetRequiredService<MetricFlowAspNetCoreOptions>());

        services.TryAddSingleton<MetricTracker>(sp =>
        {
            var opt = sp.GetRequiredService<MetricFlowAspNetCoreOptions>();
            var tracker = new MetricTracker(
                topic: opt.Topic,
                topicTags: opt.TopicTags,
                samplingRate: opt.SamplingRate,
                configObservable: opt.ConfigObservable,
                additionalCounters: opt.Counters);

            if (opt.AutoAddExceptionCounter)
            {
                tracker.AddExceptionCounter();
            }

            return tracker;
        });

        services.TryAddSingleton<IMetricTracker>(sp => sp.GetRequiredService<MetricTracker>());
        services.TryAddSingleton<IMetricSnapshotsSource>(sp => sp.GetRequiredService<MetricTracker>());

        return services;
    }

    /// <summary>
    /// Registers MetricFlow with a specific topic name and optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricFlow(
        this IServiceCollection services,
        string topic,
        Action<MetricFlowAspNetCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddMetricFlow(options =>
        {
            options.Topic = topic;
            configure(options);
        });
    }

    /// <summary>
    /// Registers a <see cref="HybridTimelineStore"/> as both <see cref="IMetricTimelineStore"/> and <see cref="IMetricSink"/>.
    /// </summary>
    public static IServiceCollection AddHybridTimelineStore(
        this IServiceCollection services,
        Action<HybridTimelineStoreOptions>? configure = null)
    {
        var options = new HybridTimelineStoreOptions();
        configure?.Invoke(options);

        var store = new HybridTimelineStore(options);
        services.AddSingleton<HybridTimelineStore>(store);
        services.AddSingleton<IMetricTimelineStore>(store);
        services.AddSingleton<IMetricSink>(store);

        return services;
    }

    /// <summary>
    /// Registers the <see cref="PeriodicMetricExporter"/> and hooks its lifecycle into ASP.NET Core hosted background services.
    /// </summary>
    public static IServiceCollection AddPeriodicExporter(
        this IServiceCollection services,
        Action<PeriodicMetricExporterOptions>? configure = null)
    {
        var options = new PeriodicMetricExporterOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<PeriodicMetricExporter>(sp =>
        {
            var source = sp.GetRequiredService<IMetricSnapshotsSource>();
            var sinks = sp.GetServices<IMetricSink>();
            var opt = sp.GetRequiredService<PeriodicMetricExporterOptions>();
            return new PeriodicMetricExporter(source, sinks, opt);
        });

        services.AddHostedService<MetricFlowExporterHostedService>();
        return services;
    }
}
