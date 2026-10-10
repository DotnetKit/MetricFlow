using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Hierarchy;

namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// A high-performance, structured console log sink that formats and writes metric snapshots to stdout or a configured <see cref="TextWriter"/>.
/// </summary>
public class ConsoleMetricSink : IMetricSink
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiGray = "\u001b[90m";
    private const string AnsiCyan = "\u001b[36m";
    private const string AnsiGreen = "\u001b[32m";
    private const string AnsiYellow = "\u001b[33m";
    private const string AnsiRed = "\u001b[31m";
    private const string AnsiBold = "\u001b[1m";

    private readonly ConsoleMetricSinkOptions _options;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets the sink options.
    /// </summary>
    public ConsoleMetricSinkOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ConsoleMetricSink"/> with optional configuration.
    /// </summary>
    /// <param name="options">Configuration options, or null for defaults.</param>
    /// <param name="name">Optional friendly name for this sink.</param>
    public ConsoleMetricSink(ConsoleMetricSinkOptions? options = null, string name = "Console")
    {
        _options = options ?? new ConsoleMetricSinkOptions();
        Name = string.IsNullOrWhiteSpace(name) ? "Console" : name;
    }

    /// <inheritdoc />
    public void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var writer = _options.OutputWriter ?? System.Console.Out;

        for (int i = 0; i < snapshots.Count; i++)
        {
            var line = FormatSnapshot(snapshots[i]);
            writer.WriteLine(line);
        }
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> to its corresponding ANSI foreground escape sequence.
    /// </summary>
    /// <param name="color">The console color to convert.</param>
    /// <returns>An ANSI escape sequence string.</returns>
    public static string ToAnsi(ConsoleColor color) => color switch
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

    /// <summary>
    /// Formats a single metric snapshot into a log line according to configured options.
    /// </summary>
    /// <param name="snapshot">The snapshot to format.</param>
    /// <returns>A formatted string line.</returns>
    public string FormatSnapshot(IMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder(128);
        bool color = _options.Colorize;
        ConsoleColor? resolvedColor = color ? _options.ResolveColor(snapshot) : null;

        // Timestamp
        if (_options.IncludeTimestamp)
        {
            var ts = snapshot.Timestamp.ToString(_options.TimestampFormat);
            if (color) sb.Append(AnsiGray);
            sb.Append('[').Append(ts).Append("] ");
            if (color) sb.Append(AnsiReset);
        }

        // Prefix
        if (!string.IsNullOrEmpty(_options.Prefix))
        {
            if (color) sb.Append(AnsiCyan);
            sb.Append(_options.Prefix).Append(' ');
            if (color) sb.Append(AnsiReset);
        }

        // Target: [CounterName:MetricName]
        if (color)
        {
            sb.Append(AnsiBold);
            sb.Append(resolvedColor.HasValue ? ToAnsi(resolvedColor.Value) : AnsiYellow);
        }
        sb.Append('[').Append(snapshot.CounterName).Append(':').Append(snapshot.MetricName).Append(']');
        if (color) sb.Append(AnsiReset);

        sb.Append(' ');

        // Details based on snapshot type
        AppendSnapshotDetails(sb, snapshot, color, resolvedColor, _options);

        return sb.ToString();
    }

    private static void AppendSnapshotDetails(
        StringBuilder sb,
        IMetricSnapshot snapshot,
        bool color,
        ConsoleColor? resolvedColor,
        ConsoleMetricSinkOptions options)
    {
        if (options.TryFormatCustom(snapshot, out var customDetails))
        {
            if (color && resolvedColor.HasValue) sb.Append(ToAnsi(resolvedColor.Value));
            sb.Append(customDetails);
            if (color && resolvedColor.HasValue) sb.Append(AnsiReset);
            return;
        }

        switch (snapshot)
        {
            case DurationSnapshot d:
                sb.Append("Avg: ");
                if (color && resolvedColor.HasValue) sb.Append(ToAnsi(resolvedColor.Value));
                sb.Append(options.FormatDuration(d.AverageDuration));
                if (color && resolvedColor.HasValue) sb.Append(AnsiReset);

                sb.Append(", Min: ").Append(options.FormatDuration(d.MinDuration))
                  .Append(", Max: ").Append(options.FormatDuration(d.MaxDuration));

                if (d.P50Duration.HasValue || d.P90Duration.HasValue || d.P95Duration.HasValue || d.P99Duration.HasValue)
                {
                    sb.Append(" (");
                    var hasPrev = false;
                    if (d.P50Duration.HasValue)
                    {
                        sb.Append("p50: ").Append(options.FormatDuration(d.P50Duration.Value));
                        hasPrev = true;
                    }
                    if (d.P90Duration.HasValue)
                    {
                        if (hasPrev) sb.Append(", ");
                        sb.Append("p90: ").Append(options.FormatDuration(d.P90Duration.Value));
                        hasPrev = true;
                    }
                    if (d.P95Duration.HasValue)
                    {
                        if (hasPrev) sb.Append(", ");
                        sb.Append("p95: ").Append(options.FormatDuration(d.P95Duration.Value));
                        hasPrev = true;
                    }
                    if (d.P99Duration.HasValue)
                    {
                        if (hasPrev) sb.Append(", ");
                        sb.Append("p99: ").Append(options.FormatDuration(d.P99Duration.Value));
                    }
                    sb.Append(')');
                }

                sb.Append(", Total: ").Append(options.FormatDuration(d.TotalDuration));

                if (d.FailedCount > 0)
                {
                    sb.Append(", ");
                    if (color) sb.Append(AnsiRed);
                    sb.Append("Failed: ").Append(d.FailedCount);
                    if (color) sb.Append(AnsiReset);
                }
                break;

            case ThroughputSnapshot tp:
                sb.Append("TotalItems: ").Append(tp.TotalItems)
                  .Append(", Operations: ").Append(tp.TotalOperations)
                  .Append(", Rate: ");
                if (color && resolvedColor.HasValue) sb.Append(ToAnsi(resolvedColor.Value));
                sb.Append(tp.ItemsPerSecond.ToString("F1", CultureInfo.InvariantCulture)).Append(' ').Append(options.ThroughputUnit);
                if (color && resolvedColor.HasValue) sb.Append(AnsiReset);

                if (tp.FailedOperations > 0)
                {
                    sb.Append(", ");
                    if (color) sb.Append(AnsiRed);
                    sb.Append("Failed: ").Append(tp.FailedOperations);
                    if (color) sb.Append(AnsiReset);
                }
                break;

            case FailureSnapshot f:
                sb.Append("Operations: ").Append(f.TotalOperations)
                  .Append(", Failed: ");

                if (f.TotalFailures > 0)
                {
                    if (color) sb.Append(resolvedColor.HasValue ? ToAnsi(resolvedColor.Value) : AnsiRed);
                    sb.Append(f.TotalFailures).Append(" (").Append((f.FailureRate * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("%)");
                    if (color) sb.Append(AnsiReset);
                }
                else
                {
                    if (color) sb.Append(resolvedColor.HasValue ? ToAnsi(resolvedColor.Value) : AnsiGreen);
                    sb.Append("0 (0.0%)");
                    if (color) sb.Append(AnsiReset);
                }
                break;

            case ExceptionSnapshot ex:
                sb.Append("Operations: ").Append(ex.TotalOperations)
                  .Append(", Exceptions: ");

                if (ex.TotalExceptions > 0)
                {
                    if (color) sb.Append(resolvedColor.HasValue ? ToAnsi(resolvedColor.Value) : AnsiRed);
                    sb.Append(ex.TotalExceptions).Append(" (").Append((ex.ExceptionRate * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("%)");
                    if (color) sb.Append(AnsiReset);
                }
                else
                {
                    if (color) sb.Append(resolvedColor.HasValue ? ToAnsi(resolvedColor.Value) : AnsiGreen);
                    sb.Append("0 (0.0%)");
                    if (color) sb.Append(AnsiReset);
                }
                break;

            case MemorySnapshot m:
                sb.Append("Operations: ").Append(m.OperationCount)
                  .Append(", Avg: ");
                if (color && resolvedColor.HasValue) sb.Append(ToAnsi(resolvedColor.Value));
                sb.Append(options.FormatMemory(m.AverageAllocatedBytes));
                if (color && resolvedColor.HasValue) sb.Append(AnsiReset);
                sb.Append(", Total: ").Append(options.FormatMemory(m.TotalAllocatedBytes));
                break;

            case HierarchyTreeSnapshot tree:
                if (options.ShowHierarchicalTree)
                {
                    sb.AppendLine();
                    var formattedTree = tree.ToFormattedString(
                        color,
                        node => ResolveNodeColor(node, options),
                        options.FormatDuration,
                        options.FormatMemory);
                    sb.Append(formattedTree.TrimEnd('\r', '\n'));
                }
                else
                {
                    sb.Append("Root: ").Append(tree.Root.MetricName)
                      .Append(", Duration: ").Append(options.FormatDuration(tree.Root.Duration))
                      .Append(", Nodes: ").Append(CountNodes(tree.Root));
                }
                break;

            default:
                // Fallback to formatted string cleaned of excess newlines for single-line display
                var formatted = snapshot.ToFormattedString();
                if (string.IsNullOrWhiteSpace(formatted))
                {
                    sb.Append("(no data)");
                }
                else
                {
                    var lines = formatted.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                    if (color && resolvedColor.HasValue) sb.Append(ToAnsi(resolvedColor.Value));
                    sb.Append(string.Join(" | ", lines));
                    if (color && resolvedColor.HasValue) sb.Append(AnsiReset);
                }
                break;
        }
    }

    private static ConsoleColor? ResolveNodeColor(HierarchyNode node, ConsoleMetricSinkOptions options)
    {
        if (options.ColorSelector != null || options.ThresholdRules.Count > 0)
        {
            var dummyDuration = new DurationSnapshot(
                MetricName: node.MetricName,
                CounterName: "Duration",
                InCount: 1,
                OutCount: 1,
                FailedCount: node.Failed ? 1 : 0,
                AverageDuration: node.Duration,
                MinDuration: node.Duration,
                MaxDuration: node.Duration,
                TotalDuration: node.Duration,
                Timestamp: node.StartTimeUtc);

            var resolved = options.ResolveColor(dummyDuration);
            if (resolved.HasValue) return resolved.Value;

            if (node.AllocatedBytes > 0)
            {
                var dummyMemory = new MemorySnapshot(
                    MetricName: node.MetricName,
                    CounterName: "Memory",
                    OperationCount: 1,
                    TotalAllocatedBytes: node.AllocatedBytes,
                    AverageAllocatedBytes: node.AllocatedBytes,
                    MinAllocatedBytes: node.AllocatedBytes,
                    MaxAllocatedBytes: node.AllocatedBytes,
                    Timestamp: node.StartTimeUtc);

                var memColor = options.ResolveColor(dummyMemory);
                if (memColor.HasValue) return memColor.Value;
            }
        }
        return null;
    }

    private static int CountNodes(HierarchyNode node)
    {
        int count = 1;
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            count += CountNodes(children[i]);
        }
        return count;
    }
}
