using DotnetKit.MetricFlow.Abstractions.Sinks;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Configuration options for the <see cref="PeriodicMetricExporter"/>.
/// </summary>
public sealed class PeriodicMetricExporterOptions
{
    /// <summary>
    /// Gets or sets the interval at which metrics are harvested and emitted to registered sinks.
    /// Default is 15 seconds.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the resource metadata identifying the current instance, service, and environment.
    /// Default is auto-detected from environment variables and host info.
    /// </summary>
    public ResourceMetadata Resource { get; set; } = ResourceMetadata.Detect();

    /// <summary>
    /// Gets or sets whether to emit entries for metrics that had zero activity (no operations) during the harvest window.
    /// Default is false to minimize noise and bandwidth.
    /// </summary>
    public bool IncludeZeroDeltaEntries { get; set; } = false;

    /// <summary>
    /// Gets or sets global static tags attached to every emitted <see cref="MetricTimelineEntry"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? GlobalTags { get; set; }

    /// <summary>
    /// Optional callback invoked when an error occurs during harvesting or sink dispatch.
    /// </summary>
    public Action<Exception>? OnError { get; set; }
}
