using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotnetKit.MetricFlow.Sinks.Logger;

/// <summary>
/// Configuration options for <see cref="LoggerMetricSink"/>.
/// </summary>
public class LoggerMetricSinkOptions
{
    /// <summary>
    /// The <see cref="ILogger"/> instance to emit metric snapshots to.
    /// If null and configured via dependency injection, this is automatically resolved from <see cref="ILoggerFactory"/>.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// The logger category name used when creating a logger via <see cref="ILoggerFactory"/>. Defaults to "DotnetKit.MetricFlow".
    /// </summary>
    public string CategoryName { get; set; } = "DotnetKit.MetricFlow";

    /// <summary>
    /// The default <see cref="Microsoft.Extensions.Logging.LogLevel"/> used for normal metric snapshot logging. Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// The <see cref="Microsoft.Extensions.Logging.LogLevel"/> used when a snapshot contains failures or exceptions. Defaults to <see cref="LogLevel.Warning"/>.
    /// </summary>
    public LogLevel FailureLogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Optional custom delegate to select a specific <see cref="Microsoft.Extensions.Logging.LogLevel"/> dynamically based on the snapshot.
    /// If null, <see cref="FailureLogLevel"/> is used for failures, and <see cref="LogLevel"/> for normal snapshots.
    /// </summary>
    public Func<IMetricSnapshot, LogLevel>? LogLevelSelector { get; set; }

    /// <summary>
    /// The <see cref="Microsoft.Extensions.Logging.EventId"/> associated with emitted metric logs. Defaults to Id=1001, Name="MetricSnapshot".
    /// </summary>
    public EventId EventId { get; set; } = new(1001, "MetricSnapshot");

    /// <summary>
    /// Whether to include timestamps in log message text. Defaults to false because most logging providers (such as Serilog or ConsoleLogger) already provide timestamps.
    /// </summary>
    public bool IncludeTimestamp { get; set; }

    /// <summary>
    /// Custom timestamp format string when <see cref="IncludeTimestamp"/> is enabled. Defaults to "HH:mm:ss.fff".
    /// </summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss.fff";

    /// <summary>
    /// Optional prefix label to prepend to output lines (e.g. "[MetricFlow]"). Defaults to null.
    /// </summary>
    public string? Prefix { get; set; }
}
