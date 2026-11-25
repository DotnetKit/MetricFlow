using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
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

        foreach (var opName in order)
        {
            var items = groups[opName];

            ThroughputSnapshot? throughput = null;
            DurationSnapshot? duration = null;
            MemorySnapshot? memory = null;
            FailureSnapshot? failure = null;
            ExceptionSnapshot? exception = null;
            CountSnapshot? count = null;
            List<DimensionSnapshot>? dimensions = null;
            List<IMetricSnapshot>? additional = null;

            foreach (var item in items)
            {
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
                    case CountSnapshot c when count == null:
                        count = c;
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
                    AdditionalSnapshots: additional,
                    Count: count
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
        var summaries = source.GetAllSnapshots().GetOperationSummaries();

        if (source is not IMetricTracker tracker || summaries.Count == 0)
        {
            return summaries;
        }

        var hasAnyParent = false;
        var enriched = new List<OperationSummary>(summaries.Count);
        foreach (var summary in summaries)
        {
            var parent = tracker.GetParentOperation(summary.OperationName);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                hasAnyParent = true;
                enriched.Add(summary with { ParentOperation = parent });
            }
            else
            {
                enriched.Add(summary);
            }
        }

        return hasAnyParent ? OrderHierarchically(enriched) : enriched;
    }

    private static List<OperationSummary> OrderHierarchically(
        List<OperationSummary> summaries
    )
    {
        var byName = new Dictionary<string, OperationSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var summary in summaries)
        {
            byName.TryAdd(summary.OperationName, summary);
        }

        var childrenByParent = new Dictionary<string, List<OperationSummary>>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<OperationSummary>();

        foreach (var summary in summaries)
        {
            if (!string.IsNullOrWhiteSpace(summary.ParentOperation)
                && byName.ContainsKey(summary.ParentOperation)
                && !string.Equals(summary.OperationName, summary.ParentOperation, StringComparison.OrdinalIgnoreCase))
            {
                if (!childrenByParent.TryGetValue(summary.ParentOperation, out var children))
                {
                    children = [];
                    childrenByParent[summary.ParentOperation] = children;
                }
                children.Add(summary);
            }
            else
            {
                roots.Add(summary);
            }
        }

        var ordered = new List<OperationSummary>(summaries.Count);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(OperationSummary node, int depth)
        {
            if (!visited.Add(node.OperationName))
            {
                return;
            }

            ordered.Add(depth > 0 ? node with { Depth = depth } : node);

            if (childrenByParent.TryGetValue(node.OperationName, out var children))
            {
                foreach (var child in children)
                {
                    Visit(child, depth + 1);
                }
            }
        }

        foreach (var root in roots)
        {
            Visit(root, depth: 0);
        }

        // Guard against cycles where every node referenced another node in the cycle
        foreach (var summary in summaries.Where(s => !visited.Contains(s.OperationName)))
        {
            Visit(summary, depth: 0);
        }

        return ordered;
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
        if (summaries.Count == 0)
        {
            return null;
        }

        var parent = tracker.GetParentOperation(operationName);
        return !string.IsNullOrWhiteSpace(parent)
            ? summaries[0] with { ParentOperation = parent }
            : summaries[0];
    }
}
