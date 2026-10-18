namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Specifies the formatting and layout mode for <see cref="ConsoleMetricSink"/>.
/// </summary>
public enum ConsoleDisplayMode
{
    /// <summary>
    /// Emits one log line per snapshot per counter. This is the default mode.
    /// </summary>
    FlatLog = 0,

    /// <summary>
    /// Aggregates snapshots with identical metric/stage names into an auto-padded, multi-column table.
    /// </summary>
    Table = 1,

    /// <summary>
    /// Formats snapshots as a GitHub-flavored Markdown table.
    /// </summary>
    Markdown = 2
}
