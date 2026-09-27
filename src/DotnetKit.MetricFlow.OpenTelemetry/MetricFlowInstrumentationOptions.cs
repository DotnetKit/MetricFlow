namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Options for configuring MetricFlow OpenTelemetry instrumentation.
/// </summary>
public class MetricFlowInstrumentationOptions
{
    /// <summary>
    /// Meter name pattern or prefix to register with OpenTelemetry. Defaults to "DotnetKit.MetricFlow.*".
    /// </summary>
    public string MeterNamePattern { get; set; } = "DotnetKit.MetricFlow.*";

    /// <summary>
    /// Base meter prefix when expanding specific topics. Defaults to "DotnetKit.MetricFlow".
    /// </summary>
    public string MeterPrefix { get; set; } = "DotnetKit.MetricFlow";

    /// <summary>
    /// Specific topic names to register. If empty, the wildcard pattern (<see cref="MeterNamePattern"/>) is used.
    /// </summary>
    public IList<string> Topics { get; } = new List<string>();

    /// <summary>
    /// Whether in-flight active operations should be recorded via UpDownCounter instruments. Defaults to true.
    /// </summary>
    public bool RecordActiveOperations { get; set; } = true;
}
