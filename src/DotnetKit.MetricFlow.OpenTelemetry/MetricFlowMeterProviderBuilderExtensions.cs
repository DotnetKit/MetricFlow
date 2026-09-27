using OpenTelemetry.Metrics;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Extension methods for configuring MetricFlow instrumentation on OpenTelemetry <see cref="MeterProviderBuilder"/>.
/// </summary>
public static class MetricFlowMeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds MetricFlow metric instrumentation to the OpenTelemetry <see cref="MeterProviderBuilder"/>.
    /// Automatically subscribes OpenTelemetry to meters produced by MetricFlow trackers.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <param name="configure">Optional configuration action for MetricFlow instrumentation options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static MeterProviderBuilder AddMetricFlowInstrumentation(
        this MeterProviderBuilder builder,
        Action<MetricFlowInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new MetricFlowInstrumentationOptions();
        configure?.Invoke(options);

        if (options.Topics.Count > 0)
        {
            foreach (var topic in options.Topics)
            {
                var meterName = string.IsNullOrWhiteSpace(options.MeterPrefix)
                    ? topic
                    : $"{options.MeterPrefix}.{topic}";
                builder.AddMeter(meterName);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(options.MeterNamePattern))
            {
                builder.AddMeter(options.MeterNamePattern);
            }

            if (!string.IsNullOrWhiteSpace(options.MeterPrefix))
            {
                builder.AddMeter(options.MeterPrefix);
            }
        }

        return builder;
    }
}
