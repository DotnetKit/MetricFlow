using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Abstractions.Sinks;

/// <summary>
/// Represents a captured snapshot of a metric over a specific time window,
/// containing both cumulative lifetime metrics and interval delta metrics.
/// </summary>
public sealed record MetricTimelineEntry(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    IMetricSnapshot Cumulative,
    IMetricSnapshot Delta,
    ResourceMetadata? Resource = null,
    IReadOnlyDictionary<string, string>? Tags = null)
{
    public string MetricName => Cumulative.MetricName;
    public string CounterName => Cumulative.CounterName;
    public TimeSpan PeriodDuration => PeriodEnd - PeriodStart;
}
