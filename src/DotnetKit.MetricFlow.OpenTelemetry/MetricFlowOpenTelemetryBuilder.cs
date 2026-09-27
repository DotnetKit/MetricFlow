using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Default implementation of <see cref="IMetricFlowOpenTelemetryBuilder"/>.
/// </summary>
public class MetricFlowOpenTelemetryBuilder : IMetricFlowOpenTelemetryBuilder
{
    private Action<MetricFlowInstrumentationOptions>? _instrumentationConfig;

    /// <inheritdoc />
    public IServiceCollection Current { get; }

    /// <summary>
    /// Gets whether metrics have been configured on this builder.
    /// </summary>
    public bool MetricsConfigured { get; private set; }

    /// <summary>
    /// Gets whether tracing has been configured on this builder.
    /// </summary>
    public bool TracingConfigured { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricFlowOpenTelemetryBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public MetricFlowOpenTelemetryBuilder(IServiceCollection services)
    {
        Current = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IMetricFlowOpenTelemetryBuilder ConfigureInstrumentation(
        Action<MetricFlowInstrumentationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _instrumentationConfig += configure;
        return this;
    }

    /// <inheritdoc />
    public IMetricFlowOpenTelemetryBuilder WithMetrics(
        Action<MeterProviderBuilder>? configure = null,
        Action<MetricFlowInstrumentationOptions>? configureMetricFlow = null)
    {
        if (configureMetricFlow != null)
        {
            _instrumentationConfig += configureMetricFlow;
        }

        MetricsConfigured = true;

        Current.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddMetricFlowInstrumentation(_instrumentationConfig);
                configure?.Invoke(metrics);
            });

        return this;
    }

    /// <inheritdoc />
    public IMetricFlowOpenTelemetryBuilder WithTracing(
        Action<TracerProviderBuilder>? configure = null)
    {
        TracingConfigured = true;

        Current.AddOpenTelemetry()
            .WithTracing(traces =>
            {
                traces.AddMetricFlowInstrumentation();
                configure?.Invoke(traces);
            });

        return this;
    }
}
