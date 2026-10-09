namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Configuration options for <see cref="ConsoleMetricSink"/>.
/// </summary>
public class ConsoleMetricSinkOptions
{
    /// <summary>
    /// The <see cref="TextWriter"/> to write log output to. If null, defaults to <see cref="System.Console.Out"/>.
    /// Setting this to a custom writer is useful for unit tests or redirecting console streams.
    /// </summary>
    public TextWriter? OutputWriter { get; set; }

    /// <summary>
    /// Whether to enable ANSI color formatting in output. Defaults to true.
    /// </summary>
    public bool Colorize { get; set; } = true;

    /// <summary>
    /// Whether to include timestamps in log output. Defaults to true.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Custom timestamp format string. Defaults to "HH:mm:ss.fff".
    /// </summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss.fff";

    /// <summary>
    /// Whether to include topic-level tags in output. Defaults to true.
    /// </summary>
    public bool IncludeTopicTags { get; set; } = true;

    /// <summary>
    /// Optional prefix label to prepend to output lines (e.g. "[MetricFlow]"). Defaults to "[MetricFlow]".
    /// </summary>
    public string Prefix { get; set; } = "[MetricFlow]";
}
