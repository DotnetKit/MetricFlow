using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Presentation;

namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Formats correlated <see cref="OperationSummary"/> collections into an indented visual parent-child execution tree
/// (<c>▼</c>, <c>├─</c>, <c>└─</c>) with exclusive self-duration and self-memory calculations.
/// </summary>
public static class TreeMetricFormatter
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiGray = "\u001b[90m";
    private const string AnsiCyan = "\u001b[36m";
    private const string AnsiRed = "\u001b[31m";
    private const string AnsiBold = "\u001b[1m";

    /// <summary>
    /// Renders a collection of operation summaries into a hierarchical execution tree string.
    /// </summary>
    /// <param name="operations">The correlated operation summaries (with optional <see cref="OperationSummary.ParentOperation"/>).</param>
    /// <param name="options">Optional console sink formatting options (units, ANSI colors, thresholds).</param>
    /// <returns>The formatted tree string.</returns>
    public static string RenderTree(
        IReadOnlyList<OperationSummary> operations,
        ConsoleMetricSinkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0)
        {
            return string.Empty;
        }

        var opts = options ?? new ConsoleMetricSinkOptions { Colorize = false };
        var color = opts.Colorize;

        var byName = new Dictionary<string, OperationSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in operations)
        {
            byName.TryAdd(op.OperationName, op);
        }

        var childrenByParent = new Dictionary<string, List<OperationSummary>>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<OperationSummary>();

        foreach (var op in operations)
        {
            if (!string.IsNullOrWhiteSpace(op.ParentOperation)
                && byName.ContainsKey(op.ParentOperation)
                && !string.Equals(op.OperationName, op.ParentOperation, StringComparison.OrdinalIgnoreCase))
            {
                if (!childrenByParent.TryGetValue(op.ParentOperation, out var children))
                {
                    children = [];
                    childrenByParent[op.ParentOperation] = children;
                }
                children.Add(op);
            }
            else
            {
                roots.Add(op);
            }
        }

        var sb = new StringBuilder();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (opts.IncludeTimestamp || !string.IsNullOrEmpty(opts.Prefix))
        {
            if (opts.IncludeTimestamp)
            {
                var ts = DateTime.UtcNow.ToString(opts.TimestampFormat, CultureInfo.InvariantCulture);
                if (color) sb.Append(AnsiGray);
                sb.Append('[').Append(ts).Append("] ");
                if (color) sb.Append(AnsiReset);
            }

            if (!string.IsNullOrEmpty(opts.Prefix))
            {
                if (color) sb.Append(AnsiCyan);
                sb.Append(opts.Prefix);
                if (color) sb.Append(AnsiReset);
            }

            sb.AppendLine();
        }

        for (var i = 0; i < roots.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }
            RenderNode(sb, roots[i], childrenByParent, visited, opts, color, indent: "", isLast: true, isRoot: true);
        }

        // Guard against cycles where every node referenced another node in the cycle
        foreach (var op in operations.Where(o => !visited.Contains(o.OperationName)))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }
            RenderNode(sb, op, childrenByParent, visited, opts, color, indent: "", isLast: true, isRoot: true);
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void RenderNode(
        StringBuilder sb,
        OperationSummary node,
        Dictionary<string, List<OperationSummary>> childrenByParent,
        HashSet<string> visited,
        ConsoleMetricSinkOptions opts,
        bool color,
        string indent,
        bool isLast,
        bool isRoot)
    {
        if (!visited.Add(node.OperationName))
        {
            return;
        }

        var hasChildren = childrenByParent.TryGetValue(node.OperationName, out var children) && children.Count > 0;

        // Tree branch connector
        if (color) sb.Append(AnsiGray);
        if (isRoot)
        {
            sb.Append("▼ ");
        }
        else
        {
            sb.Append(indent).Append(isLast ? "└─ " : "├─ ");
        }
        if (color) sb.Append(AnsiReset);

        // Operation name + count suffix when executed multiple times
        if (color) sb.Append(AnsiBold).Append(AnsiCyan);
        sb.Append('[').Append(node.OperationName).Append(']');
        if (color) sb.Append(AnsiReset);

        if (node.TotalOperations > 1)
        {
            if (color) sb.Append(AnsiGray);
            sb.Append(" x").Append(node.TotalOperations.ToString("N0", CultureInfo.InvariantCulture));
            if (color) sb.Append(AnsiReset);
        }

        // Metrics payload: (duration, self | items | alloc, self)
        var segments = new List<string>(4);

        if (node.Duration is { } dur)
        {
            var durSb = new StringBuilder();
            var durColor = color ? opts.ResolveColor(dur) : null;
            if (durColor.HasValue) durSb.Append(ConsoleMetricSink.ToAnsi(durColor.Value));
            durSb.Append(opts.FormatDuration(dur.TotalDuration));
            if (durColor.HasValue) durSb.Append(AnsiReset);

            if (hasChildren)
            {
                var childrenTicks = children!.Where(static c => c.Duration != null).Sum(static c => c.Duration!.TotalDuration.Ticks);
                if (childrenTicks > 0)
                {
                    var selfDuration = dur.TotalDuration.Ticks > childrenTicks
                        ? TimeSpan.FromTicks(dur.TotalDuration.Ticks - childrenTicks)
                        : TimeSpan.Zero;
                    durSb.Append(", self: ").Append(opts.FormatDuration(selfDuration));
                }
            }
            else if (dur.OutCount > 1)
            {
                durSb.Append(", avg: ").Append(opts.FormatDuration(dur.AverageDuration));
            }

            segments.Add(durSb.ToString());
        }

        if (node.Throughput is { TotalItems: > 0 } tp)
        {
            segments.Add($"items: {tp.TotalItems.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        if (node.Memory is { TotalAllocatedBytes: > 0 } mem)
        {
            var memSb = new StringBuilder();
            memSb.Append("alloc: ");
            var memColor = color ? opts.ResolveColor(mem) : null;
            if (memColor.HasValue) memSb.Append(ConsoleMetricSink.ToAnsi(memColor.Value));
            memSb.Append(opts.FormatMemory(mem.TotalAllocatedBytes));
            if (memColor.HasValue) memSb.Append(AnsiReset);

            if (hasChildren)
            {
                var childrenBytes = children!.Where(static c => c.Memory != null).Sum(static c => c.Memory!.TotalAllocatedBytes);
                if (childrenBytes > 0)
                {
                    var selfBytes = Math.Max(0, mem.TotalAllocatedBytes - childrenBytes);
                    memSb.Append(", self: ").Append(opts.FormatMemory(selfBytes));
                }
            }

            segments.Add(memSb.ToString());
        }

        if (node.TotalFailures > 0)
        {
            var failText = $"FAILED: {node.TotalFailures.ToString(CultureInfo.InvariantCulture)}";
            segments.Add(color ? $"{AnsiRed}{failText}{AnsiReset}" : failText);
        }

        if (segments.Count > 0)
        {
            sb.Append(" (").Append(string.Join(" | ", segments)).Append(')');
        }

        sb.AppendLine();

        if (!hasChildren)
        {
            return;
        }

        string childIndent;
        if (isRoot)
        {
            childIndent = "  ";
        }
        else
        {
            childIndent = isLast ? $"{indent}    " : $"{indent}│   ";
        }

        for (var i = 0; i < children!.Count; i++)
        {
            RenderNode(
                sb,
                children[i],
                childrenByParent,
                visited,
                opts,
                color,
                childIndent,
                isLast: i == children.Count - 1,
                isRoot: false);
        }
    }
}
