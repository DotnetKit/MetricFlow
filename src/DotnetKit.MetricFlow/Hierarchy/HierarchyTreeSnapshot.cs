using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Hierarchy;

/// <summary>
/// Snapshot representing a complete hierarchical execution tree of correlated parent-child operations.
/// </summary>
/// <param name="MetricName">The name of the root metric operation.</param>
/// <param name="CounterName">The counter name, typically "Hierarchy".</param>
/// <param name="Root">The root node of the execution hierarchy.</param>
/// <param name="Timestamp">The timestamp when the root operation completed.</param>
public record HierarchyTreeSnapshot(
    string MetricName,
    string CounterName,
    HierarchyNode Root,
    DateTime Timestamp) : IMetricSnapshot
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiBold = "\u001b[1m";
    private const string AnsiGray = "\u001b[90m";
    private const string AnsiYellow = "\u001b[33m";
    private const string AnsiRed = "\u001b[31m";
    private const string AnsiCyan = "\u001b[36m";

    /// <inheritdoc />
    public string ToFormattedString() => ToFormattedString(colorize: false, colorSelector: null);

    /// <summary>
    /// Formats the hierarchical execution tree with optional ANSI colorization and custom node coloring.
    /// </summary>
    /// <param name="colorize">Whether to apply ANSI color sequences.</param>
    /// <param name="colorSelector">Optional delegate to choose colors for specific nodes.</param>
    /// <returns>A multi-line formatted string representing the execution tree.</returns>
    public string ToFormattedString(bool colorize, Func<HierarchyNode, ConsoleColor?>? colorSelector = null)
    {
        var sb = new StringBuilder();
        FormatNode(sb, Root, indent: "", isLast: true, isRoot: true, colorize: colorize, colorSelector: colorSelector);
        return sb.ToString();
    }

    private static void FormatNode(
        StringBuilder sb,
        HierarchyNode node,
        string indent,
        bool isLast,
        bool isRoot,
        bool colorize,
        Func<HierarchyNode, ConsoleColor?>? colorSelector)
    {
        var nodeColor = colorize && colorSelector != null ? colorSelector(node) : null;

        if (isRoot)
        {
            if (colorize) sb.Append(AnsiBold).Append(AnsiCyan);
            sb.Append("▼ ");
            if (colorize) sb.Append(AnsiReset);
        }
        else
        {
            sb.Append(indent);
            if (colorize) sb.Append(AnsiGray);
            sb.Append(isLast ? "└─ " : "├─ ");
            if (colorize) sb.Append(AnsiReset);
        }

        // Operation name
        if (colorize)
        {
            sb.Append(AnsiBold);
            if (node.Failed)
            {
                sb.Append(AnsiRed);
            }
            else if (nodeColor.HasValue)
            {
                sb.Append(ToAnsi(nodeColor.Value));
            }
            else
            {
                sb.Append(AnsiYellow);
            }
        }
        sb.Append('[').Append(node.MetricName).Append(']');
        if (colorize) sb.Append(AnsiReset);

        sb.Append(" (");

        // Duration
        if (colorize && nodeColor.HasValue) sb.Append(ToAnsi(nodeColor.Value));
        sb.Append(node.Duration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)).Append(" ms");
        if (colorize && nodeColor.HasValue) sb.Append(AnsiReset);

        // Self-duration (only when node has children)
        if (node.Children.Count > 0)
        {
            sb.Append(", self: ").Append(node.SelfDuration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)).Append(" ms");
        }

        // Items
        if (node.ItemCount > 0)
        {
            sb.Append(" | items: ").Append(node.ItemCount);
        }

        // Allocated bytes
        if (node.AllocatedBytes > 0)
        {
            sb.Append(" | alloc: ").Append(FormatBytes(node.AllocatedBytes));
            if (node.Children.Count > 0 && node.SelfAllocatedBytes > 0)
            {
                sb.Append(", self: ").Append(FormatBytes(node.SelfAllocatedBytes));
            }
        }

        // Failure / Exception
        if (node.Failed)
        {
            sb.Append(" | ");
            if (colorize) sb.Append(AnsiRed).Append(AnsiBold);
            sb.Append("FAIL");
            if (node.Exception != null)
            {
                sb.Append(": ").Append(node.Exception.GetType().Name);
                if (!string.IsNullOrWhiteSpace(node.Exception.Message))
                {
                    sb.Append(" (\"").Append(node.Exception.Message).Append("\")");
                }
            }
            if (colorize) sb.Append(AnsiReset);
        }

        sb.Append(')');
        sb.AppendLine();

        // Children
        string childIndent;
        if (isRoot)
        {
            childIndent = "  ";
        }
        else
        {
            var branch = isLast ? "    " : "│   ";
            childIndent = indent + branch;
        }

        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            bool isLastChild = i == children.Count - 1;
            FormatNode(sb, children[i], childIndent, isLastChild, isRoot: false, colorize: colorize, colorSelector: colorSelector);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0).ToString("F2", CultureInfo.InvariantCulture)} KB";
        return $"{(bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture)} MB";
    }

    private static string ToAnsi(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => "\u001b[30m",
        ConsoleColor.DarkBlue => "\u001b[34m",
        ConsoleColor.DarkGreen => "\u001b[32m",
        ConsoleColor.DarkCyan => "\u001b[36m",
        ConsoleColor.DarkRed => "\u001b[31m",
        ConsoleColor.DarkMagenta => "\u001b[35m",
        ConsoleColor.DarkYellow => "\u001b[33m",
        ConsoleColor.Gray => "\u001b[37m",
        ConsoleColor.DarkGray => "\u001b[90m",
        ConsoleColor.Blue => "\u001b[94m",
        ConsoleColor.Green => "\u001b[32m",
        ConsoleColor.Cyan => "\u001b[36m",
        ConsoleColor.Red => "\u001b[31m",
        ConsoleColor.Magenta => "\u001b[35m",
        ConsoleColor.Yellow => "\u001b[33m",
        ConsoleColor.White => "\u001b[97m",
        _ => "\u001b[0m"
    };

    /// <inheritdoc />
    public override string ToString() => ToFormattedString();
}
