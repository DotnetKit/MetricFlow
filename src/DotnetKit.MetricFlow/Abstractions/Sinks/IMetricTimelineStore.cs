using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Abstractions.Sinks;

/// <summary>
/// Defines a timeline store that persists metric timeline entries and provides time-range queries.
/// </summary>
public interface IMetricTimelineStore : IMetricSink
{
    /// <summary>
    /// Retrieves timeline entries recorded between the specified UTC time range.
    /// </summary>
    /// <param name="fromUtc">The start of the time range (inclusive).</param>
    /// <param name="toUtc">The end of the time range (inclusive).</param>
    /// <param name="metricName">Optional metric name filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask<IReadOnlyList<MetricTimelineEntry>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes aggregated metric snapshots over the specified UTC time range by merging delta slices.
    /// </summary>
    /// <param name="fromUtc">The start of the time range (inclusive).</param>
    /// <param name="toUtc">The end of the time range (inclusive).</param>
    /// <param name="metricName">Optional metric name filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask<IReadOnlyList<IMetricSnapshot>> GetAggregatedSnapshotsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default);
}
