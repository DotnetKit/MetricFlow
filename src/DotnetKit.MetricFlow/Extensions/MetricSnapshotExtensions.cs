using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Extensions;

/// <summary>
/// Extension methods for formatting metric snapshots and snapshot sources.
/// </summary>
public static class MetricSnapshotExtensions
{
    /// <summary>
    /// Formats a collection of metric snapshots grouped by metric operation name.
    /// </summary>
    /// <param name="snapshots">The collection of metric snapshots to format.</param>
    /// <returns>A formatted string containing all snapshots grouped by metric name.</returns>
    public static string ToFormattedString(this IEnumerable<IMetricSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var sb = new StringBuilder();
        var snapshotsByOperation = snapshots
            .GroupBy(s => s.MetricName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in snapshotsByOperation)
        {
            foreach (var snapshot in group)
            {
                sb.AppendLine(snapshot.ToFormattedString());
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats all metric snapshots from the snapshot source grouped by metric operation name.
    /// </summary>
    /// <param name="source">The metric snapshot source.</param>
    /// <returns>A formatted string containing all snapshots from the source.</returns>
    public static string ToFormattedString(this IMetricSnapshotsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().ToFormattedString();
    }
}
