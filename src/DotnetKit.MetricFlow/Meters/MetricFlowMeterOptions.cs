namespace DotnetKit.MetricFlow.Meters;

/// <summary>
/// Options for configuring System.Diagnostics.Metrics integration in MetricFlow.
/// </summary>
public class MetricFlowMeterOptions
{
    /// <summary>
    /// Whether System.Diagnostics.Metrics emission is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Meter prefix for naming meters. Defaults to "DotnetKit.MetricFlow".
    /// Resulting meter name will be "{MeterPrefix}.{Topic}" (or "{MeterPrefix}" if Topic is empty).
    /// </summary>
    public string MeterPrefix { get; set; } = "DotnetKit.MetricFlow";

    /// <summary>
    /// Whether to include topic-level tags in emitted instrument measurements. Defaults to true.
    /// </summary>
    public bool IncludeTopicTags { get; set; } = true;

    /// <summary>
    /// Whether to enforce cardinality limits on tags emitted to System.Diagnostics.Metrics.
    /// Clamps tag values exceeding <see cref="MaxUniqueTagValues"/> to <see cref="OverflowBucket"/>.
    /// Defaults to true.
    /// </summary>
    public bool EnforceCardinalityLimitsOnMeters { get; set; } = true;

    /// <summary>
    /// Maximum number of unique values allowed per tag key before rolling over to <see cref="OverflowBucket"/>.
    /// Defaults to 250.
    /// </summary>
    public int MaxUniqueTagValues { get; set; } = 250;

    /// <summary>
    /// The overflow bucket name used when cardinality exceeds <see cref="MaxUniqueTagValues"/>.
    /// Defaults to "[Other]".
    /// </summary>
    public string OverflowBucket { get; set; } = "[Other]";

    /// <summary>
    /// Whether to record in-flight operations via UpDownCounter instruments ("{metricName}.active").
    /// Defaults to true.
    /// </summary>
    public bool RecordActiveOperations { get; set; } = true;

    /// <summary>
    /// Whether to always record an items instrument measurement (defaulting to 1 if not specified).
    /// If false, items measurements are only recorded when an item count is provided.
    /// Defaults to false.
    /// </summary>
    public bool AlwaysRecordItems { get; set; } = false;

    /// <summary>
    /// Instrument naming convention. Defaults to <see cref="MetricInstrumentNamingConvention.PerMetricName"/>.
    /// </summary>
    public MetricInstrumentNamingConvention NamingConvention { get; set; } = MetricInstrumentNamingConvention.PerMetricName;

    /// <summary>
    /// Per-tag-key cardinality limits overriding <see cref="MaxUniqueTagValues"/>.
    /// </summary>
    public Dictionary<string, int> TagCardinalityLimits { get; } = new(StringComparer.OrdinalIgnoreCase);
}
