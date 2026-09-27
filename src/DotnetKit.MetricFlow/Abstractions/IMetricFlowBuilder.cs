using Microsoft.Extensions.DependencyInjection;

namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Builder for configuring MetricFlow core services and topic trackers using a fluent API.
/// </summary>
public interface IMetricFlowBuilder : IFluentBuilder<IServiceCollection>, IServiceCollection
{
    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    IServiceCollection Services => Current;

    /// <summary>
    /// Adds a metric tracker for a specific topic.
    /// </summary>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddMetricTracker(string topic, Action<MetricFlowOptions>? configure = null);

    /// <summary>
    /// Adds a default metric tracker.
    /// </summary>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddMetricTracker(Action<MetricFlowOptions>? configure = null); 
}
