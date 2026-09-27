using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Provides factory and helper methods for creating and configuring OpenTelemetry telemetry pre-configured with MetricFlow.
/// </summary>
public static class MetricFlowTelemetry
{
    /// <summary>
    /// Creates and builds an OpenTelemetry <see cref="MeterProvider"/> pre-configured with MetricFlow metric instrumentation.
    /// </summary>
    /// <param name="configure">Optional configuration action for the <see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configureMetricFlow">Optional configuration action for MetricFlow instrumentation options.</param>
    /// <returns>A built <see cref="MeterProvider"/> instance.</returns>
    public static MeterProvider CreateMeterProvider(
        Action<MeterProviderBuilder>? configure = null,
        Action<MetricFlowInstrumentationOptions>? configureMetricFlow = null)
    {
        var builder = Sdk.CreateMeterProviderBuilder()
            .AddMetricFlowInstrumentation(configureMetricFlow);

        configure?.Invoke(builder);

        return builder.Build();
    }

    /// <summary>
    /// Creates a pre-configured <see cref="MeterProviderBuilder"/> with MetricFlow metric instrumentation attached.
    /// </summary>
    /// <param name="configureMetricFlow">Optional configuration action for MetricFlow instrumentation options.</param>
    /// <returns>A new <see cref="MeterProviderBuilder"/> instance with MetricFlow instrumentation.</returns>
    public static MeterProviderBuilder CreateMeterProviderBuilder(
        Action<MetricFlowInstrumentationOptions>? configureMetricFlow = null)
    {
        return Sdk.CreateMeterProviderBuilder()
            .AddMetricFlowInstrumentation(configureMetricFlow);
    }
}
