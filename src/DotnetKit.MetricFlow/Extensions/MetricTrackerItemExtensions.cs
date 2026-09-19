using System.Runtime.CompilerServices;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for <see cref="IMetricTracker"/> to support tracking operations with item/batch counts for throughput tracking.
/// </summary>
public static class MetricTrackerItemExtensions
{
    /// <summary>
    /// Tracks an operation with an associated item/entity count for throughput calculation.
    /// </summary>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="itemCount">The number of items or records processed in this operation.</param>
    /// <param name="additionalTags">Optional additional business tags.</param>
    /// <param name="additionalMetadata">Optional additional technical metadata.</param>
    /// <returns>A tracking scope disposable.</returns>
    public static IDisposable TrackItems(
        this IMetricTracker tracker,
        string metricName,
        long itemCount,
        Dictionary<string, string>? additionalTags = null,
        Dictionary<string, long>? additionalMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        var metadata = additionalMetadata != null ? new Dictionary<string, long>(additionalMetadata) : new Dictionary<string, long>();
        metadata["items"] = itemCount;
        return tracker.Track(metricName, additionalTags, metadata);
    }

    /// <summary>
    /// Tracks an operation with an associated item/entity count with the metric name resolved dynamically via [CallerMemberName].
    /// </summary>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="itemCount">The number of items or records processed in this operation.</param>
    /// <param name="additionalTags">Optional additional business tags.</param>
    /// <param name="additionalMetadata">Optional additional technical metadata.</param>
    /// <param name="metricName">The calling member name.</param>
    /// <returns>A tracking scope disposable.</returns>
    public static IDisposable TrackItems(
        this IMetricTracker tracker,
        long itemCount,
        Dictionary<string, string>? additionalTags = null,
        Dictionary<string, long>? additionalMetadata = null,
        [CallerMemberName] string metricName = "")
    {
        return TrackItems(tracker, metricName, itemCount, additionalTags, additionalMetadata);
    }
}
