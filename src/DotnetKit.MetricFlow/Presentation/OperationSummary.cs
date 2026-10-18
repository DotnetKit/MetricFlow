using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Hierarchy;

namespace DotnetKit.MetricFlow.Presentation;

/// <summary>
/// Aggregates all counter snapshots associated with a single operation into a correlated summary.
/// </summary>
public record OperationSummary(
    string OperationName,
    ThroughputSnapshot? Throughput = null,
    DurationSnapshot? Duration = null,
    MemorySnapshot? Memory = null,
    FailureSnapshot? Failure = null,
    ExceptionSnapshot? Exception = null,
    IReadOnlyList<DimensionSnapshot>? Dimensions = null,
    HierarchyTreeSnapshot? Hierarchy = null,
    IReadOnlyList<IMetricSnapshot>? AdditionalSnapshots = null
)
{
    /// <summary>
    /// Gets all categorical dimension snapshots tracked under this operation.
    /// </summary>
    public IReadOnlyList<DimensionSnapshot> Dimensions { get; init; } = Dimensions ?? [];

    /// <summary>
    /// Gets any additional or custom metric snapshots associated with this operation.
    /// </summary>
    public IReadOnlyList<IMetricSnapshot> AdditionalSnapshots { get; init; } =
        AdditionalSnapshots ?? [];

    /// <summary>
    /// Gets the total operation count recorded across the primary counters.
    /// </summary>
    public long TotalOperations =>
        Throughput?.TotalOperations
        ?? Duration?.OutCount
        ?? Memory?.OperationCount
        ?? Failure?.TotalOperations
        ?? Exception?.TotalOperations
        ?? 0;

    /// <summary>
    /// Gets the total failures or exceptions recorded for this operation.
    /// </summary>
    public long TotalFailures =>
        Failure?.TotalFailures
        ?? Exception?.TotalExceptions
        ?? Duration?.FailedCount
        ?? Throughput?.FailedOperations
        ?? 0;

    /// <summary>
    /// Gets the failure rate (between 0.0 and 1.0) recorded for this operation.
    /// </summary>
    public double FailureRate =>
        Failure?.FailureRate
        ?? Exception?.ExceptionRate
        ?? (TotalOperations > 0 ? (double)TotalFailures / TotalOperations : 0.0);

    /// <summary>
    /// Gets the average execution duration, if recorded.
    /// </summary>
    public TimeSpan? AverageDuration => Duration?.AverageDuration;

    /// <summary>
    /// Gets the throughput processing rate in items per second, if recorded.
    /// </summary>
    public double? ItemsPerSecond => Throughput?.ItemsPerSecond;

    /// <summary>
    /// Gets the average memory allocation in bytes per operation, if recorded.
    /// </summary>
    public long? AverageAllocatedBytes => Memory?.AverageAllocatedBytes;
}
