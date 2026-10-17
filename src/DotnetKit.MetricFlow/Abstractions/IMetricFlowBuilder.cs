using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// Adds a <see cref="ConsoleMetricSink"/> for structured console logging.
    /// </summary>
    /// <param name="configure">Optional configuration for console output formatting.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddConsoleSink(Action<ConsoleMetricSinkOptions>? configure = null);

    /// <summary>
    /// Adds a <see cref="LoggerMetricSink"/> for structured logging via <see cref="ILogger"/>.
    /// The logger is automatically resolved from the dependency injection container (<see cref="ILoggerFactory"/> or <see cref="ILogger"/>).
    /// </summary>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddLoggerSink(Action<LoggerMetricSinkOptions>? configure = null);

    /// <summary>
    /// Adds a <see cref="LoggerMetricSink"/> for structured logging via a specific <see cref="ILogger"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddLoggerSink(ILogger logger, Action<LoggerMetricSinkOptions>? configure = null);

    /// <summary>
    /// Adds a custom metric sink to receive snapshot emissions.
    /// </summary>
    /// <param name="sink">The metric sink instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowBuilder AddSink(IMetricSink sink);
}

