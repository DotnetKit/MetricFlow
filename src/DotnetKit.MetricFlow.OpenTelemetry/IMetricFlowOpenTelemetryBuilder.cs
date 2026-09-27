using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Sub-fluent builder for configuring OpenTelemetry integration within MetricFlow.
/// </summary>
public interface IMetricFlowOpenTelemetryBuilder : IFluentBuilder<IServiceCollection>
{
    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    IServiceCollection Services => Current;

    /// <summary>
    /// Configures the OpenTelemetry metric pipeline and automatically binds MetricFlow instruments.
    /// </summary>
    /// <param name="configure">Optional configuration action for the <see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configureMetricFlow">Optional configuration action for MetricFlow instrumentation options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowOpenTelemetryBuilder WithMetrics(
        Action<MeterProviderBuilder>? configure = null,
        Action<MetricFlowInstrumentationOptions>? configureMetricFlow = null);

    /// <summary>
    /// Configures the OpenTelemetry trace pipeline and correlates MetricFlow activity sources.
    /// </summary>
    /// <param name="configure">Optional configuration action for the <see cref="TracerProviderBuilder"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowOpenTelemetryBuilder WithTracing(
        Action<TracerProviderBuilder>? configure = null);
    
    /// <summary>
    /// Configures MetricFlow OpenTelemetry instrumentation options.
    /// </summary>
    /// <param name="configure">Configuration delegate for instrumentation options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IMetricFlowOpenTelemetryBuilder ConfigureInstrumentation(
        Action<MetricFlowInstrumentationOptions> configure);
}
