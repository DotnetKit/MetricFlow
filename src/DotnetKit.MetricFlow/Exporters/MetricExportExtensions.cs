using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Presentation;
using DotnetKit.MetricFlow.Sinks.Console;

namespace DotnetKit.MetricFlow.Exporters;

/// <summary>
/// Extension methods for exporting metric snapshots and operation summaries to Markdown, CSV, and JSON formats.
/// </summary>
public static class MetricExportExtensions
{
    #region Markdown Table Export

    /// <summary>
    /// Exports all metric snapshots from the source as a formatted GitHub-flavored Markdown table.
    /// </summary>
    /// <param name="source">The metric snapshots source.</param>
    /// <param name="options">Optional Markdown formatting options.</param>
    /// <returns>A GitHub-flavored Markdown table string.</returns>
    public static string ToMarkdownTable(
        this IMetricSnapshotsSource source,
        MarkdownTableOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().ToMarkdownTable(options);
    }

    /// <summary>
    /// Exports a collection of metric snapshots as a formatted GitHub-flavored Markdown table.
    /// </summary>
    /// <param name="snapshots">The collection of metric snapshots.</param>
    /// <param name="options">Optional Markdown formatting options.</param>
    /// <returns>A GitHub-flavored Markdown table string.</returns>
    public static string ToMarkdownTable(
        this IEnumerable<IMetricSnapshot> snapshots,
        MarkdownTableOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return snapshots.GetOperationSummaries().ToMarkdownTable(options);
    }

    /// <summary>
    /// Exports a collection of correlated operation summaries as a formatted GitHub-flavored Markdown table.
    /// </summary>
    /// <param name="operations">The collection of operation summaries.</param>
    /// <param name="options">Optional Markdown formatting options.</param>
    /// <returns>A GitHub-flavored Markdown table string.</returns>
    public static string ToMarkdownTable(
        this IEnumerable<OperationSummary> operations,
        MarkdownTableOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(operations);
        var opList = operations as IReadOnlyList<OperationSummary> ?? [.. operations];
        var opt = options ?? new MarkdownTableOptions();

        if (opList.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(opt.Title))
        {
            sb.AppendLine($"### {opt.Title}");
            sb.AppendLine();
        }

        if (opt.IncludeHeaders)
        {
            sb.AppendLine(
                "| Operation | Operations | Duration (Avg) | Min / Max | p95 / p99 | Throughput Rate | Memory (Avg) | Failures |"
            );
            sb.AppendLine("| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        }

        var culture = CultureInfo.InvariantCulture;

        foreach (var op in opList)
        {
            var ops = op.TotalOperations.ToString("N0", culture);
            var durAvg =
                op.Duration != null ? FormatDuration(op.Duration.AverageDuration, opt) : "-";
            var minMax = "-";
            var p95p99 = "-";
            if (op.Duration != null)
            {
                var minStr = FormatDuration(op.Duration.MinDuration, opt);
                var maxStr = FormatDuration(op.Duration.MaxDuration, opt);
                minMax = $"{minStr} / {maxStr}";

                var p95Str = op.Duration.P95Duration.HasValue
                    ? FormatDuration(op.Duration.P95Duration.Value, opt)
                    : "-";
                var p99Str = op.Duration.P99Duration.HasValue
                    ? FormatDuration(op.Duration.P99Duration.Value, opt)
                    : "-";
                p95p99 = $"{p95Str} / {p99Str}";
            }
            var tpRate =
                op.Throughput != null
                    ? $"{op.Throughput.ItemsPerSecond.ToString("F1", culture)} {opt.ThroughputUnit}"
                    : "-";
            var mem = op.Memory != null ? FormatBytes(op.Memory.AverageAllocatedBytes) : "-";
            var fail = $"{op.TotalFailures:N0} ({(op.FailureRate * 100).ToString("F1", culture)}%)";

            sb.Append("| ")
                .Append(EscapeMarkdown(op.OperationName))
                .Append(" | ")
                .Append(ops)
                .Append(" | ")
                .Append(durAvg)
                .Append(" | ")
                .Append(minMax)
                .Append(" | ")
                .Append(p95p99)
                .Append(" | ")
                .Append(tpRate)
                .Append(" | ")
                .Append(mem)
                .Append(" | ")
                .Append(fail)
                .AppendLine(" |");

            if (opt.IncludeDimensions && op.Dimensions.Count > 0)
            {
                foreach (var dim in op.Dimensions)
                {
                    var items = dim
                        .Breakdown.OrderByDescending(kv => kv.Value)
                        .Take(5)
                        .Select(kv =>
                        {
                            var pct =
                                dim.TotalOperations > 0
                                    ? (double)kv.Value / dim.TotalOperations * 100
                                    : 0.0;
                            return $"{kv.Key} ({pct.ToString("F1", culture)}%)";
                        });

                    sb.Append("| └─ *")
                        .Append(dim.DimensionName)
                        .Append("*: ")
                        .Append(string.Join(", ", items))
                        .AppendLine(" | | | | | | | |");
                }
            }
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    #endregion

    #region CSV Export

    /// <summary>
    /// Exports all metric snapshots from the source as RFC 4180 compliant CSV text.
    /// </summary>
    /// <param name="source">The metric snapshots source.</param>
    /// <param name="options">Optional CSV export options.</param>
    /// <returns>A CSV formatted string.</returns>
    public static string ToCsv(this IMetricSnapshotsSource source, CsvExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().ToCsv(options);
    }

    /// <summary>
    /// Exports a collection of metric snapshots as RFC 4180 compliant CSV text.
    /// </summary>
    /// <param name="snapshots">The collection of metric snapshots.</param>
    /// <param name="options">Optional CSV export options.</param>
    /// <returns>A CSV formatted string.</returns>
    public static string ToCsv(
        this IEnumerable<IMetricSnapshot> snapshots,
        CsvExportOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return snapshots.GetOperationSummaries().ToCsv(options);
    }

    /// <summary>
    /// Exports a collection of correlated operation summaries as RFC 4180 compliant CSV text.
    /// </summary>
    /// <param name="operations">The collection of operation summaries.</param>
    /// <param name="options">Optional CSV export options.</param>
    /// <returns>A CSV formatted string.</returns>
    public static string ToCsv(
        this IEnumerable<OperationSummary> operations,
        CsvExportOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(operations);
        var opList = operations as IReadOnlyList<OperationSummary> ?? operations.ToList();
        var opt = options ?? new CsvExportOptions();
        var d = opt.Delimiter;
        var culture = CultureInfo.InvariantCulture;

        var sb = new StringBuilder();

        if (opt.IncludeHeaders)
        {
            sb.Append("Operation")
                .Append(d)
                .Append("TotalOperations")
                .Append(d)
                .Append("DurationAvgMs")
                .Append(d)
                .Append("DurationMinMs")
                .Append(d)
                .Append("DurationMaxMs")
                .Append(d)
                .Append("DurationP50Ms")
                .Append(d)
                .Append("DurationP90Ms")
                .Append(d)
                .Append("DurationP95Ms")
                .Append(d)
                .Append("DurationP99Ms")
                .Append(d)
                .Append("DurationTotalMs")
                .Append(d)
                .Append("ThroughputTotalItems")
                .Append(d)
                .Append("ThroughputRate")
                .Append(d)
                .Append("MemoryAvgBytes")
                .Append(d)
                .Append("MemoryTotalBytes")
                .Append(d)
                .Append("TotalFailures")
                .Append(d)
                .Append("FailureRate");

            if (opt.IncludeDimensions)
            {
                sb.Append(d).Append("Dimensions");
            }

            sb.AppendLine();
        }

        foreach (var s in opList)
        {
            var dAvg = s.Duration?.AverageDuration.TotalMilliseconds.ToString("F3", culture) ?? "0";
            var dMin = s.Duration?.MinDuration.TotalMilliseconds.ToString("F3", culture) ?? "0";
            var dMax = s.Duration?.MaxDuration.TotalMilliseconds.ToString("F3", culture) ?? "0";
            var dP50 = s.Duration?.P50Duration?.TotalMilliseconds.ToString("F3", culture) ?? "";
            var dP90 = s.Duration?.P90Duration?.TotalMilliseconds.ToString("F3", culture) ?? "";
            var dP95 = s.Duration?.P95Duration?.TotalMilliseconds.ToString("F3", culture) ?? "";
            var dP99 = s.Duration?.P99Duration?.TotalMilliseconds.ToString("F3", culture) ?? "";
            var dTot = s.Duration?.TotalDuration.TotalMilliseconds.ToString("F3", culture) ?? "0";

            var tpItems = s.Throughput?.TotalItems.ToString(culture) ?? "0";
            var tpRate = s.Throughput?.ItemsPerSecond.ToString("F2", culture) ?? "0";

            var memAvg = s.Memory?.AverageAllocatedBytes.ToString(culture) ?? "0";
            var memTot = s.Memory?.TotalAllocatedBytes.ToString(culture) ?? "0";

            var fails = s.TotalFailures.ToString(culture);
            var fRate = s.FailureRate.ToString("F4", culture);

            sb.Append(EscapeCsv(s.OperationName, d))
                .Append(d)
                .Append(s.TotalOperations.ToString(culture))
                .Append(d)
                .Append(dAvg)
                .Append(d)
                .Append(dMin)
                .Append(d)
                .Append(dMax)
                .Append(d)
                .Append(dP50)
                .Append(d)
                .Append(dP90)
                .Append(d)
                .Append(dP95)
                .Append(d)
                .Append(dP99)
                .Append(d)
                .Append(dTot)
                .Append(d)
                .Append(tpItems)
                .Append(d)
                .Append(tpRate)
                .Append(d)
                .Append(memAvg)
                .Append(d)
                .Append(memTot)
                .Append(d)
                .Append(fails)
                .Append(d)
                .Append(fRate);

            if (opt.IncludeDimensions)
            {
                var dimParts = s.Dimensions.Select(dim =>
                {
                    var pairs = dim.Breakdown.Select(kv => $"{kv.Key}:{kv.Value}");
                    return $"{dim.DimensionName}=[{string.Join(";", pairs)}]";
                });
                sb.Append(d).Append(EscapeCsv(string.Join("|", dimParts), d));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    #endregion

    #region JSON Export

    /// <summary>
    /// Serializes all metric snapshots from the source into a structured JSON string.
    /// </summary>
    /// <param name="source">The metric snapshots source.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    /// <returns>A formatted JSON string representing operation metrics.</returns>
    public static string ToJson(
        this IMetricSnapshotsSource source,
        JsonExportOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetAllSnapshots().ToJson(options);
    }

    /// <summary>
    /// Serializes a collection of metric snapshots into a structured JSON string.
    /// </summary>
    /// <param name="snapshots">The collection of metric snapshots.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    /// <returns>A formatted JSON string representing operation metrics.</returns>
    public static string ToJson(
        this IEnumerable<IMetricSnapshot> snapshots,
        JsonExportOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return snapshots.GetOperationSummaries().ToJson(options);
    }

    /// <summary>
    /// Serializes a collection of correlated operation summaries into a structured JSON string.
    /// </summary>
    /// <param name="operations">The collection of operation summaries.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    /// <returns>A formatted JSON string representing operation metrics.</returns>
    public static string ToJson(
        this IEnumerable<OperationSummary> operations,
        JsonExportOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(operations);
        var opList = operations as IReadOnlyList<OperationSummary> ?? operations.ToList();
        var opt = options ?? new JsonExportOptions();

        var jsonOptions = GetJsonSerializerOptions(opt);

        var exportDocument = new MetricExportDocument(
            ExportedAtUtc: DateTime.UtcNow,
            TotalOperations: opList.Count,
            Operations: opList
                .Select(s => new OperationExportDto(
                    OperationName: s.OperationName,
                    TotalOperations: s.TotalOperations,
                    TotalFailures: s.TotalFailures,
                    FailureRate: s.FailureRate,
                    AverageDurationMs: s.Duration?.AverageDuration.TotalMilliseconds,
                    MinDurationMs: s.Duration?.MinDuration.TotalMilliseconds,
                    MaxDurationMs: s.Duration?.MaxDuration.TotalMilliseconds,
                    P95DurationMs: s.Duration?.P95Duration?.TotalMilliseconds,
                    P99DurationMs: s.Duration?.P99Duration?.TotalMilliseconds,
                    TotalDurationMs: s.Duration?.TotalDuration.TotalMilliseconds,
                    TotalItems: s.Throughput?.TotalItems,
                    ItemsPerSecond: s.Throughput?.ItemsPerSecond,
                    AverageAllocatedBytes: s.Memory?.AverageAllocatedBytes,
                    TotalAllocatedBytes: s.Memory?.TotalAllocatedBytes,
                    Dimensions: s.Dimensions.Count > 0
                        ? s.Dimensions.ToDictionary(d => d.DimensionName, d => d.Breakdown)
                        : null
                ))
                .ToList()
        );

        return JsonSerializer.Serialize(exportDocument, jsonOptions);
    }

    private sealed record MetricExportDocument(
        DateTime ExportedAtUtc,
        int TotalOperations,
        IReadOnlyList<OperationExportDto> Operations
    );

    private sealed record OperationExportDto(
        string OperationName,
        long TotalOperations,
        long TotalFailures,
        double FailureRate,
        double? AverageDurationMs,
        double? MinDurationMs,
        double? MaxDurationMs,
        double? P95DurationMs,
        double? P99DurationMs,
        double? TotalDurationMs,
        long? TotalItems,
        double? ItemsPerSecond,
        long? AverageAllocatedBytes,
        long? TotalAllocatedBytes,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>? Dimensions
    );

    #endregion

    #region Helper Methods

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string EscapeCsv(string text, string delimiter)
    {
        if (
            text.Contains(delimiter, StringComparison.Ordinal)
            || text.Contains('"', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal)
            || text.Contains('\r', StringComparison.Ordinal)
        )
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return text;
    }

    private static string FormatDuration(TimeSpan duration, MarkdownTableOptions options)
    {
        var culture = CultureInfo.InvariantCulture;
        var format = $"F{options.DurationDecimals}";

        return options.DurationUnit switch
        {
            DurationUnit.Milliseconds =>
                $"{duration.TotalMilliseconds.ToString(format, culture)} ms",
            DurationUnit.Seconds => $"{duration.TotalSeconds.ToString(format, culture)} s",
            DurationUnit.Minutes => $"{duration.TotalMinutes.ToString(format, culture)} m",
            DurationUnit.Hours => $"{duration.TotalHours.ToString(format, culture)} h",
            _ => $"{duration.TotalMilliseconds.ToString(format, culture)} ms",
        };
    }

    private static string FormatBytes(long bytes)
    {
        var culture = CultureInfo.InvariantCulture;
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{(bytes / 1024.0).ToString("F2", culture)} KB",
            < 1024 * 1024 * 1024 => $"{(bytes / (1024.0 * 1024.0)).ToString("F2", culture)} MB",
            _ => $"{(bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2", culture)} GB",
        };
    }

    private static readonly JsonSerializerOptions IndentedCamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions CompactCamelCase = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions IndentedExact = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions CompactExact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonSerializerOptions GetJsonSerializerOptions(JsonExportOptions options)
    {
        if (options.CamelCase)
        {
            return options.Indented ? IndentedCamelCase : CompactCamelCase;
        }

        return options.Indented ? IndentedExact : CompactExact;
    }

    #endregion
}
