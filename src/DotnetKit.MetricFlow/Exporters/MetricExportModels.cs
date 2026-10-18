using DotnetKit.MetricFlow.Sinks.Console;

namespace DotnetKit.MetricFlow.Exporters;

/// <summary>
/// Configuration options for exporting metric summaries as a GitHub-flavored Markdown table.
/// </summary>
public sealed class MarkdownTableOptions
{
    /// <summary>
    /// Gets or sets an optional title heading to render above the Markdown table.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to include the table header and alignment row. Defaults to true.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Whether to render categorical dimension breakdowns beneath each operation. Defaults to true.
    /// </summary>
    public bool IncludeDimensions { get; set; } = true;

    /// <summary>
    /// Unit used for formatting duration values. Defaults to <see cref="DurationUnit.Milliseconds"/>.
    /// </summary>
    public DurationUnit DurationUnit { get; set; } = DurationUnit.Milliseconds;

    /// <summary>
    /// Number of decimal places for formatted durations. Defaults to 2.
    /// </summary>
    public int DurationDecimals { get; set; } = 2;

    /// <summary>
    /// Label for throughput rate. Defaults to "items/s".
    /// </summary>
    public string ThroughputUnit { get; set; } = "items/s";
}

/// <summary>
/// Configuration options for exporting metric summaries as CSV text.
/// </summary>
public sealed class CsvExportOptions
{
    /// <summary>
    /// The column delimiter character. Defaults to comma (",").
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Whether to output the CSV header row. Defaults to true.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Whether to include serialized dimension tags in the CSV output. Defaults to true.
    /// </summary>
    public bool IncludeDimensions { get; set; } = true;
}

/// <summary>
/// Configuration options for exporting metric summaries as JSON text.
/// </summary>
public sealed class JsonExportOptions
{
    /// <summary>
    /// Whether the output JSON should be formatted with indentation and whitespace. Defaults to true.
    /// </summary>
    public bool Indented { get; set; } = true;

    /// <summary>
    /// Whether property names should use camelCase naming policy. Defaults to true.
    /// </summary>
    public bool CamelCase { get; set; } = true;
}
