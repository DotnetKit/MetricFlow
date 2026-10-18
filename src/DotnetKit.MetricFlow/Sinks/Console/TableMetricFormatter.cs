using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Presentation;

namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Formats operation summaries into structured, auto-padded console tables with cell-level ANSI threshold colorization
/// and dimension breakdown sub-rows.
/// </summary>
public static class TableMetricFormatter
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiGray = "\u001b[90m";
    private const string AnsiCyan = "\u001b[36m";
    private const string AnsiBold = "\u001b[1m";

    /// <summary>
    /// Renders a collection of operation summaries as an auto-padded console table.
    /// </summary>
    /// <param name="operations">The operation summaries to render.</param>
    /// <param name="options">The console sink options.</param>
    /// <returns>A formatted multi-line table string.</returns>
    public static string RenderTable(
        IReadOnlyList<OperationSummary> operations,
        ConsoleMetricSinkOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(options);

        if (operations.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var color = options.Colorize;

        // Render optional title banner if prefix or timestamp is configured
        if (!string.IsNullOrEmpty(options.Prefix) || options.IncludeTimestamp)
        {
            if (options.IncludeTimestamp)
            {
                var ts = DateTime.UtcNow.ToString(
                    options.TimestampFormat,
                    CultureInfo.InvariantCulture
                );
                if (color)
                    sb.Append(AnsiGray);
                sb.Append('[').Append(ts).Append("] ");
                if (color)
                    sb.Append(AnsiReset);
            }

            if (!string.IsNullOrEmpty(options.Prefix))
            {
                if (color)
                    sb.Append(AnsiCyan);
                sb.Append(options.Prefix).Append(' ');
                if (color)
                    sb.Append(AnsiReset);
            }

            sb.AppendLine("Metric Summary Table");
        }

        // Determine which columns are active across all operations
        var hasDuration = operations.Any(s => s.Duration != null);
        var hasThroughput = operations.Any(s => s.Throughput != null);
        var hasMemory = operations.Any(s => s.Memory != null);
        var hasFailures = operations.Any(s =>
            s.Failure != null || s.Exception != null || s.TotalFailures > 0
        );

        // Precompute raw strings and colors per cell
        var rows = new List<TableRowData>(operations.Count);
        var maxOpLen = "Operation".Length;
        var maxOpsLen = "Operations".Length;
        var maxDurLen = hasDuration ? "Duration (Avg / p99)".Length : 0;
        var maxTpLen = hasThroughput ? "Throughput Rate".Length : 0;
        var maxMemLen = hasMemory ? "Memory (Avg / Total)".Length : 0;
        var maxFailLen = hasFailures ? "Failures".Length : 0;

        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];

            // 1. Operation column
            var opText = op.OperationName;
            if (opText.Length > maxOpLen)
                maxOpLen = opText.Length;

            // 2. Operations count column
            var opsCountText = op.TotalOperations.ToString("N0", CultureInfo.InvariantCulture);
            if (opsCountText.Length > maxOpsLen)
                maxOpsLen = opsCountText.Length;

            // 3. Duration column
            var durText = "-";
            ConsoleColor? durColor = null;
            if (op.Duration != null)
            {
                durText = FormatDurationCell(op.Duration, options);
                durColor = color ? options.ResolveColor(op.Duration) : null;
            }
            if (hasDuration && durText.Length > maxDurLen)
                maxDurLen = durText.Length;

            // 4. Throughput column
            var tpText = "-";
            ConsoleColor? tpColor = null;
            if (op.Throughput != null)
            {
                tpText =
                    $"{op.Throughput.ItemsPerSecond.ToString("F1", CultureInfo.InvariantCulture)} {options.ThroughputUnit}";
                tpColor = color ? options.ResolveColor(op.Throughput) : null;
            }
            if (hasThroughput && tpText.Length > maxTpLen)
                maxTpLen = tpText.Length;

            // 5. Memory column
            var memText = "-";
            ConsoleColor? memColor = null;
            if (op.Memory != null)
            {
                var avg = options.FormatMemory(op.Memory.AverageAllocatedBytes);
                var tot = options.FormatMemory(op.Memory.TotalAllocatedBytes);
                memText = $"{avg} / {tot}";
                memColor = color ? options.ResolveColor(op.Memory) : null;
            }
            if (hasMemory && memText.Length > maxMemLen)
                maxMemLen = memText.Length;

            // 6. Failures column
            var failText = "-";
            ConsoleColor? failColor = null;
            if (hasFailures)
            {
                var totalFail = op.TotalFailures;
                var rate = (op.FailureRate * 100).ToString("F1", CultureInfo.InvariantCulture);
                failText = $"{totalFail:N0} ({rate}%)";

                if (op.Failure != null)
                {
                    failColor = color ? options.ResolveColor(op.Failure) : null;
                }
                if (!failColor.HasValue && color)
                {
                    failColor = totalFail > 0 ? ConsoleColor.Red : ConsoleColor.Green;
                }
            }
            if (hasFailures && failText.Length > maxFailLen)
                maxFailLen = failText.Length;

            rows.Add(
                new TableRowData(
                    op,
                    opText,
                    opsCountText,
                    durText,
                    durColor,
                    tpText,
                    tpColor,
                    memText,
                    memColor,
                    failText,
                    failColor
                )
            );
        }

        // Render Header
        if (options.ShowTableHeaders)
        {
            if (color)
                sb.Append(AnsiBold);
            sb.Append(PadRight("Operation", maxOpLen));
            sb.Append(" | ").Append(PadLeft("Operations", maxOpsLen));
            if (hasDuration)
                sb.Append(" | ").Append(PadLeft("Duration (Avg / p99)", maxDurLen));
            if (hasThroughput)
                sb.Append(" | ").Append(PadLeft("Throughput Rate", maxTpLen));
            if (hasMemory)
                sb.Append(" | ").Append(PadLeft("Memory (Avg / Total)", maxMemLen));
            if (hasFailures)
                sb.Append(" | ").Append(PadLeft("Failures", maxFailLen));
            if (color)
                sb.Append(AnsiReset);
            sb.AppendLine();

            // Separator line
            if (color)
                sb.Append(AnsiGray);
            sb.Append(new string('-', maxOpLen));
            sb.Append("-+-").Append(new string('-', maxOpsLen));
            if (hasDuration)
                sb.Append("-+-").Append(new string('-', maxDurLen));
            if (hasThroughput)
                sb.Append("-+-").Append(new string('-', maxTpLen));
            if (hasMemory)
                sb.Append("-+-").Append(new string('-', maxMemLen));
            if (hasFailures)
                sb.Append("-+-").Append(new string('-', maxFailLen));
            if (color)
                sb.Append(AnsiReset);
            sb.AppendLine();
        }

        // Render Rows
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Operation (left-aligned)
            if (color)
                sb.Append(AnsiCyan);
            sb.Append(row.OpText.PadRight(maxOpLen));
            if (color)
                sb.Append(AnsiReset);

            // Delimiter
            AppendDelimiter(sb, color);

            // Operations count (right-aligned)
            sb.Append(row.OpsCountText.PadLeft(maxOpsLen));

            // Duration (right-aligned, cell-level threshold color)
            if (hasDuration)
            {
                AppendDelimiter(sb, color);
                AppendCell(
                    sb,
                    row.DurationText,
                    maxDurLen,
                    row.DurationColor,
                    padLeft: true,
                    color
                );
            }

            // Throughput (right-aligned, cell-level threshold color)
            if (hasThroughput)
            {
                AppendDelimiter(sb, color);
                AppendCell(
                    sb,
                    row.ThroughputText,
                    maxTpLen,
                    row.ThroughputColor,
                    padLeft: true,
                    color
                );
            }

            // Memory (right-aligned, cell-level threshold color)
            if (hasMemory)
            {
                AppendDelimiter(sb, color);
                AppendCell(sb, row.MemoryText, maxMemLen, row.MemoryColor, padLeft: true, color);
            }

            // Failures (right-aligned, cell-level threshold color)
            if (hasFailures)
            {
                AppendDelimiter(sb, color);
                AppendCell(
                    sb,
                    row.FailuresText,
                    maxFailLen,
                    row.FailuresColor,
                    padLeft: true,
                    color
                );
            }

            sb.AppendLine();

            // Render dimension sub-rows if present
            if (options.ShowDimensionBreakdown && row.Operation.Dimensions.Count > 0)
            {
                RenderDimensionsTree(sb, row.Operation.Dimensions, options, color);
            }
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void RenderDimensionsTree(
        StringBuilder sb,
        IReadOnlyList<DimensionSnapshot> dimensions,
        ConsoleMetricSinkOptions options,
        bool color
    )
    {
        for (var d = 0; d < dimensions.Count; d++)
        {
            var dim = dimensions[d];
            var isLast = d == dimensions.Count - 1;
            var branch = isLast ? "  └─ " : "  ├─ ";

            if (color)
                sb.Append(AnsiGray);
            sb.Append(branch);
            if (color)
                sb.Append(AnsiReset);

            if (color)
                sb.Append(AnsiBold);
            sb.Append(dim.DimensionName).Append(": ");
            if (color)
                sb.Append(AnsiReset);

            // Format top items
            var ordered = dim
                .Breakdown.OrderByDescending(kvp => kvp.Value)
                .Take(options.MaxDimensionValuesToRender)
                .ToList();

            var total = dim.TotalOperations > 0 ? dim.TotalOperations : 1.0;

            for (var k = 0; k < ordered.Count; k++)
            {
                if (k > 0)
                    sb.Append(", ");
                var pct = (ordered[k].Value / total * 100.0).ToString(
                    "F1",
                    CultureInfo.InvariantCulture
                );
                sb.Append(ordered[k].Key).Append(" (").Append(pct).Append("%)");
            }

            var remaining = dim.Breakdown.Count - ordered.Count;
            if (remaining > 0)
            {
                sb.Append(", +").Append(remaining).Append(" more");
            }

            sb.AppendLine();
        }
    }

    private static string FormatDurationCell(DurationSnapshot d, ConsoleMetricSinkOptions options)
    {
        var avg = options.FormatDuration(d.AverageDuration);
        if (d.P99Duration.HasValue)
        {
            var p99 = options.FormatDuration(d.P99Duration.Value);
            return $"{avg} (p99: {p99})";
        }
        if (d.P95Duration.HasValue)
        {
            var p95 = options.FormatDuration(d.P95Duration.Value);
            return $"{avg} (p95: {p95})";
        }
        return avg;
    }

    private static void AppendDelimiter(StringBuilder sb, bool color)
    {
        if (color)
            sb.Append(AnsiGray);
        sb.Append(" | ");
        if (color)
            sb.Append(AnsiReset);
    }

    private static void AppendCell(
        StringBuilder sb,
        string text,
        int width,
        ConsoleColor? color,
        bool padLeft,
        bool enableColor
    )
    {
        var padded = padLeft ? text.PadLeft(width) : text.PadRight(width);
        if (enableColor && color.HasValue)
        {
            sb.Append(ConsoleMetricSink.ToAnsi(color.Value));
            sb.Append(padded);
            sb.Append(AnsiReset);
        }
        else
        {
            sb.Append(padded);
        }
    }

    private static string PadLeft(string text, int width) => text.PadLeft(width);

    private static string PadRight(string text, int width) => text.PadRight(width);

    private sealed record TableRowData(
        OperationSummary Operation,
        string OpText,
        string OpsCountText,
        string DurationText,
        ConsoleColor? DurationColor,
        string ThroughputText,
        ConsoleColor? ThroughputColor,
        string MemoryText,
        ConsoleColor? MemoryColor,
        string FailuresText,
        ConsoleColor? FailuresColor
    );
}
