namespace DotnetKit.MetricFlow;

/// <summary>
/// Naming conventions for System.Diagnostics.Metrics instruments.
/// </summary>
public enum MetricInstrumentNamingConvention
{
    /// <summary>
    /// Instruments are named per operation: e.g. "{metricName}.duration", "{metricName}.total".
    /// </summary>
    PerMetricName,

    /// <summary>
    /// Shared instruments named "operation.duration", "operation.total" with an "operation" tag.
    /// </summary>
    SharedOperation
}
