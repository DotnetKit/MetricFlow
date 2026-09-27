using DotnetKit.MetricFlow.OpenTelemetry;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods for configuring MetricFlow tracing on OpenTelemetry <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class MetricFlowTracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds MetricFlow activity source to OpenTelemetry trace pipeline.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static TracerProviderBuilder AddMetricFlowInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(MetricFlowTracingExtensions.DefaultActivitySourceName);
    }
}
