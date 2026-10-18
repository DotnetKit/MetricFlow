using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Hierarchy;
using DotnetKit.MetricFlow.Presentation;

// ReSharper disable once CheckNamespace
namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for correlating metric snapshots into operation summaries.
/// </summary>
public static class OperationSummaryExtensions
{
    /// <summary>
    /// Correlates a collection of metric snapshots into structured <see cref="OperationSummary"/> instances grouped by operation name.
    /// Preserves the order in which operation names first appear in the snapshot collection.
    /// </summary>
    /// <param name="snapshots">The collection of metric snapshots.</param>
    /// <returns>A read-only list of correlated operation summaries.</returns>
    public static IReadOnlyList<OperationSummary> GetOperationSummaries(
        this IEnumerable<IMetricSnapshot> snapshots
    )
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var groups = new Dictionary<string, List<IMetricSnapshot>>(
            StringComparer.OrdinalIgnoreCase
        );
        var order = new List<string>();

        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.MetricName))
            {
                continue;
            }

            if (!groups.TryGetValue(snapshot.MetricName, out var list))
            {
                list = [];
                groups[snapshot.MetricName] = list;
                order.Add(snapshot.MetricName);
            }

            list.Add(snapshot);
        }

        var summaries = new List<OperationSummary>(order.Count);

        for (int i = 0; i < order.Count; i++)
        {
            var opName = order[i];
            var items = groups[opName];

            ThroughputSnapshot? throughput = null;
            DurationSnapshot? duration = null;
            MemorySnapshot? memory = null;
            FailureSnapshot? failure = null;
            ExceptionSnapshot? exception = null;
            HierarchyTreeSnapshot? hierarchy = null;
            List<DimensionSnapshot>? dimensions = null;
            List<IMetricSnapshot>? additional = null;

            for (int j = 0; j < items.Count; j++)
            {
                var item = items[j];
                switch (item)
                {
                    case ThroughputSnapshot tp when throughput == null:
                        throughput = tp;
                        break;
                    case DurationSnapshot d when duration == null:
                        duration = d;
                        break;
                    case MemorySnapshot m when memory == null:
                        memory = m;
                        break;
                    case FailureSnapshot f when failure == null:
                        failure = f;
                        break;
                    case ExceptionSnapshot ex when exception == null:
                        exception = ex;
                        break;
                    case HierarchyTreeSnapshot h when hierarchy == null:
                        hierarchy = h;
                        break;
                    case DimensionSnapshot dim:
                        dimensions ??= [];
                        dimensions.Add(dim);
                        break;
                    default:
                        additional ??= [];
                        additional.Add(item);
                        break;
                }
            }

            summaries.Add(
                new OperationSummary(
                    OperationName: opName,
                    Throughput: throughput,
                    Duration: duration,
                    Memory: memory,
                    Failure: failure,
                    Exception: exception,
                    Dimensions: dimensions,
                    Hierarchy: hierarchy,
                    AdditionalSnapshots: additional
                )
            );
        }

        return summaries;
    }

    /// <summary>
    /// Correlates all snapshots produced by the snapshot source into structured <see cref="OperationSummary"/> instances.
    /// </summary>
    /// <param name="source">The metric snapshots source.</param>
    /// <returns>A read-only list of correlated operation summaries.</returns>
    public static IReadOnlyList<OperationSummary> GetOperationSummaries(
        this IMetricSnapshotsSource source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().GetOperationSummaries();
    }

    /// <summary>
    /// Correlates snapshots for a specific operation name into an <see cref="OperationSummary"/>.
    /// </summary>
    /// <param name="tracker">The metric tracker.</param>
    /// <param name="operationName">The operation name.</param>
    /// <returns>The correlated operation summary, or null if no snapshots exist for the operation.</returns>
    public static OperationSummary? GetOperationSummary(
        this IMetricTracker tracker,
        string operationName
    )
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(operationName);

        var snapshots = tracker.GetSnapshots(operationName).ToList();
        if (snapshots.Count == 0)
        {
            return null;
        }

        var summaries = snapshots.GetOperationSummaries();
        return summaries.Count > 0 ? summaries[0] : null;
    }
}
