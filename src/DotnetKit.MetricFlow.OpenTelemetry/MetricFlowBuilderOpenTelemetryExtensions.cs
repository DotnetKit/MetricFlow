using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry integration on <see cref="IMetricFlowBuilder"/>.
/// </summary>
public static class MetricFlowBuilderOpenTelemetryExtensions
{
    /// <summary>
    /// Configures OpenTelemetry metrics and tracing integration for MetricFlow using a sub-fluent builder.
    /// </summary>
    /// <param name="builder">The <see cref="IMetricFlowBuilder"/> instance.</param>
    /// <param name="configure">Optional configuration action for the OpenTelemetry sub-fluent builder.</param>
    /// <returns>The original <see cref="IMetricFlowBuilder"/> instance for fluent chaining.</returns>
    public static IMetricFlowBuilder WithOpenTelemetry(
        this IMetricFlowBuilder builder,
        Action<IMetricFlowOpenTelemetryBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var otelBuilder = new MetricFlowOpenTelemetryBuilder(builder.Current);

        if (configure == null)
        {
            // Default: wire up metrics with MetricFlow instrumentation automatically
            otelBuilder.WithMetrics();
        }
        else
        {
            configure(otelBuilder);

            // If the user configured instrumentation options or tracing but did not explicitly invoke WithMetrics,
            // ensure metrics with MetricFlow instrumentation are still registered by default.
            if (!otelBuilder.MetricsConfigured)
            {
                otelBuilder.WithMetrics();
            }
        }

        return builder;
    }
}
