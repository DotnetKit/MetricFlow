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

    /// <summary>
    /// Adds a <see cref="DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSink"/> for structured console logging.
    /// </summary>
    /// <param name="configure">Optional configuration for console output formatting.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddConsoleSink(Action<DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSinkOptions>? configure = null);

    /// <summary>
    /// Adds a custom metric sink to receive snapshot emissions.
    /// </summary>
    /// <param name="sink">The metric sink instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddSink(DotnetKit.MetricFlow.Sinks.IMetricSink sink);
}

