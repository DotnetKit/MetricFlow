using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for configuring MetricFlow in dependency injection.
/// </summary>
public static class MetricFlowServiceCollectionExtensions
{
    /// <summary>
    /// Registers MetricFlow and its services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricFlow(
        this IServiceCollection services,
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MetricFlowOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);

        services.TryAddSingleton<MetricTracker>(sp =>
        {
            var opt = sp.GetRequiredService<MetricFlowOptions>();
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
        Action<MetricFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        return services.AddMetricFlow(options =>
        {
            options.Topic = topic;
            configure?.Invoke(options);
        });
    }
}
