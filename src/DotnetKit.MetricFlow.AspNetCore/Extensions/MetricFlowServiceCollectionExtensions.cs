using DotnetKit.MetricFlow.Abstractions;
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
        Action<MetricFlowAspNetCoreOptions>? configure = null)
    {
        var options = new MetricFlowAspNetCoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);

        services.TryAddSingleton<MetricTracker>(sp =>
        {
            var opt = sp.GetRequiredService<MetricFlowAspNetCoreOptions>();
            var tracker = new MetricTracker(
                topic: opt.Topic,
                topicTags: opt.TopicTags,
                samplingRate: opt.SamplingRate);

            tracker.AddExceptionCounter();
            return tracker;
        });

        services.TryAddSingleton<IMetricTracker>(sp => sp.GetRequiredService<MetricTracker>());

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
        Action<MetricFlowAspNetCoreOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        return services.AddMetricFlow(options =>
        {
            options.Topic = topic;
            configure?.Invoke(options);
        });
    }
}
