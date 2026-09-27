using DotnetKit.MetricFlow;
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
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent chaining.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for ASP.NET Core options.</param>
    /// <returns>The <see cref="IMetricFlowBuilder"/> instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricFlow(
        this IServiceCollection services,
        Action<MetricFlowAspNetCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MetricFlowAspNetCoreOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<MetricFlowOptions>(sp => sp.GetRequiredService<MetricFlowAspNetCoreOptions>());

        var builder = services.AddMetricFlow();
        builder.AddMetricTracker(options.Topic, opt =>
        {
            opt.Topic = options.Topic;
            opt.TopicTags = options.TopicTags;
            opt.SamplingRate = options.SamplingRate;
            opt.ConfigObservable = options.ConfigObservable;
            opt.AutoAddExceptionCounter = options.AutoAddExceptionCounter;
            foreach (var counter in options.Counters)
            {
                opt.Counters.Add(counter);
            }
        });

        return builder;
    }

    /// <summary>
    /// Registers MetricFlow with a specific topic name and optional configuration.
    /// Returns an <see cref="IMetricFlowBuilder"/> for fluent chaining.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Configuration action for ASP.NET Core options.</param>
    /// <returns>The <see cref="IMetricFlowBuilder"/> instance for fluent chaining.</returns>
    public static IMetricFlowBuilder AddMetricFlow(
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
}
