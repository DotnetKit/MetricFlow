using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotnetKit.MetricFlow.Sinks.Logger;

/// <summary>
/// A high-performance, structured logging sink that emits metric snapshots through Microsoft.Extensions.Logging <see cref="ILogger"/>.
/// Supports structured property extraction for logging backends like Serilog, OpenTelemetry, and Seq.
/// </summary>
public class LoggerMetricSink : IMetricSink
{
    private Func<ILogger> _loggerAccessor;
    private readonly LoggerMetricSinkOptions _options;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets the sink options.
    /// </summary>
    public LoggerMetricSinkOptions Options => _options;

    internal bool NeedsLogger => _loggerAccessor() is NullLogger;

    /// <summary>
    /// Initializes a new instance of <see cref="LoggerMetricSink"/> using a specific <see cref="ILogger"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Optional sink configuration options.</param>
    /// <param name="name">Optional friendly name for this sink.</param>
    public LoggerMetricSink(ILogger logger, LoggerMetricSinkOptions? options = null, string name = "Logger")
    {
        ArgumentNullException.ThrowIfNull(logger);
        _options = options ?? new LoggerMetricSinkOptions();
        _loggerAccessor = () => logger;
        Name = string.IsNullOrWhiteSpace(name) ? "Logger" : name;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LoggerMetricSink"/> using an <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="options">Optional sink configuration options.</param>
    /// <param name="name">Optional friendly name for this sink.</param>
    public LoggerMetricSink(ILoggerFactory loggerFactory, LoggerMetricSinkOptions? options = null, string name = "Logger")
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? new LoggerMetricSinkOptions();
        _loggerAccessor = () => loggerFactory.CreateLogger(_options.CategoryName);
        Name = string.IsNullOrWhiteSpace(name) ? "Logger" : name;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LoggerMetricSink"/> using a dynamic logger accessor delegate.
    /// </summary>
    /// <param name="loggerAccessor">A delegate returning the current logger instance.</param>
    /// <param name="options">Optional sink configuration options.</param>
    /// <param name="name">Optional friendly name for this sink.</param>
    public LoggerMetricSink(Func<ILogger> loggerAccessor, LoggerMetricSinkOptions? options = null, string name = "Logger")
    {
        ArgumentNullException.ThrowIfNull(loggerAccessor);
        _loggerAccessor = loggerAccessor;
        _options = options ?? new LoggerMetricSinkOptions();
        Name = string.IsNullOrWhiteSpace(name) ? "Logger" : name;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LoggerMetricSink"/> with options. If no logger is provided, defaults to <see cref="NullLogger.Instance"/>.
    /// </summary>
    /// <param name="options">Optional sink configuration options.</param>
    /// <param name="name">Optional friendly name for this sink.</param>
    public LoggerMetricSink(LoggerMetricSinkOptions? options = null, string name = "Logger")
    {
        _options = options ?? new LoggerMetricSinkOptions();
        _loggerAccessor = () => _options.Logger ?? NullLogger.Instance;
        Name = string.IsNullOrWhiteSpace(name) ? "Logger" : name;
    }

    internal void SetLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _loggerAccessor = () => logger;
    }

    /// <inheritdoc />
    public void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var logger = _loggerAccessor();
        if (logger == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            EmitSnapshot(logger, snapshots[i]);
        }
    }

    private void EmitSnapshot(ILogger logger, IMetricSnapshot snapshot)
    {
        var level = GetLogLevel(snapshot);
        if (!logger.IsEnabled(level))
        {
            return;
        }

        bool hasPrefix = !string.IsNullOrEmpty(_options.Prefix);
        bool hasTimestamp = _options.IncludeTimestamp;

        string prefixText = BuildPrefixText(snapshot, hasTimestamp, hasPrefix);

        switch (snapshot)
        {
            case ThroughputSnapshot tp:
                if (tp.FailedOperations > 0)
                {
                    if (string.IsNullOrEmpty(prefixText))
                    {
                        logger.Log(level, _options.EventId,
                            "[{CounterName}:{MetricName}] TotalItems: {TotalItems}, Operations: {Operations}, Rate: {Rate:F1} items/s, Failed: {FailedOperations}",
                            tp.CounterName, tp.MetricName, tp.TotalItems, tp.TotalOperations, tp.ItemsPerSecond, tp.FailedOperations);
                    }
                    else
                    {
                        logger.Log(level, _options.EventId,
                            "{Prefix}[{CounterName}:{MetricName}] TotalItems: {TotalItems}, Operations: {Operations}, Rate: {Rate:F1} items/s, Failed: {FailedOperations}",
                            prefixText, tp.CounterName, tp.MetricName, tp.TotalItems, tp.TotalOperations, tp.ItemsPerSecond, tp.FailedOperations);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(prefixText))
                    {
                        logger.Log(level, _options.EventId,
                            "[{CounterName}:{MetricName}] TotalItems: {TotalItems}, Operations: {Operations}, Rate: {Rate:F1} items/s",
                            tp.CounterName, tp.MetricName, tp.TotalItems, tp.TotalOperations, tp.ItemsPerSecond);
                    }
                    else
                    {
                        logger.Log(level, _options.EventId,
                            "{Prefix}[{CounterName}:{MetricName}] TotalItems: {TotalItems}, Operations: {Operations}, Rate: {Rate:F1} items/s",
                            prefixText, tp.CounterName, tp.MetricName, tp.TotalItems, tp.TotalOperations, tp.ItemsPerSecond);
                    }
                }
                break;

            case FailureSnapshot f:
                if (string.IsNullOrEmpty(prefixText))
                {
                    logger.Log(level, _options.EventId,
                        "[{CounterName}:{MetricName}] Operations: {Operations}, Failed: {FailedOperations} ({FailureRate:F1}%)",
                        f.CounterName, f.MetricName, f.TotalOperations, f.TotalFailures, f.FailureRate * 100);
                }
                else
                {
                    logger.Log(level, _options.EventId,
                        "{Prefix}[{CounterName}:{MetricName}] Operations: {Operations}, Failed: {FailedOperations} ({FailureRate:F1}%)",
                        prefixText, f.CounterName, f.MetricName, f.TotalOperations, f.TotalFailures, f.FailureRate * 100);
                }
                break;

            default:
                var details = CleanMultiline(snapshot.ToFormattedString());
                if (string.IsNullOrEmpty(prefixText))
                {
                    logger.Log(level, _options.EventId,
                        "[{CounterName}:{MetricName}] {SnapshotDetails}",
                        snapshot.CounterName, snapshot.MetricName, details);
                }
                else
                {
                    logger.Log(level, _options.EventId,
                        "{Prefix}[{CounterName}:{MetricName}] {SnapshotDetails}",
                        prefixText, snapshot.CounterName, snapshot.MetricName, details);
                }
                break;
        }
    }

    /// <summary>
    /// Formats a single metric snapshot into a log line according to configured options.
    /// </summary>
    /// <param name="snapshot">The snapshot to format.</param>
    /// <returns>A formatted string line.</returns>
    public string FormatSnapshot(IMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder(128);

        if (_options.IncludeTimestamp)
        {
            var ts = snapshot.Timestamp.ToString(_options.TimestampFormat);
            sb.Append('[').Append(ts).Append("] ");
        }

        if (!string.IsNullOrEmpty(_options.Prefix))
        {
            sb.Append(_options.Prefix).Append(' ');
        }

        sb.Append('[').Append(snapshot.CounterName).Append(':').Append(snapshot.MetricName).Append("] ");

        switch (snapshot)
        {
            case ThroughputSnapshot tp:
                sb.Append("TotalItems: ").Append(tp.TotalItems)
                  .Append(", Operations: ").Append(tp.TotalOperations)
                  .Append(", Rate: ").Append(tp.ItemsPerSecond.ToString("F1", CultureInfo.InvariantCulture)).Append(" items/s");

                if (tp.FailedOperations > 0)
                {
                    sb.Append(", Failed: ").Append(tp.FailedOperations);
                }
                break;

            case FailureSnapshot f:
                sb.Append("Operations: ").Append(f.TotalOperations)
                  .Append(", Failed: ")
                  .Append(f.TotalFailures).Append(" (").Append((f.FailureRate * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("%)");
                break;

            default:
                var formatted = snapshot.ToFormattedString();
                sb.Append(CleanMultiline(formatted));
                break;
        }

        return sb.ToString();
    }

    private string BuildPrefixText(IMetricSnapshot snapshot, bool hasTimestamp, bool hasPrefix)
    {
        if (hasTimestamp && hasPrefix)
        {
            return $"[{snapshot.Timestamp.ToString(_options.TimestampFormat)}] {_options.Prefix} ";
        }
        if (hasTimestamp)
        {
            return $"[{snapshot.Timestamp.ToString(_options.TimestampFormat)}] ";
        }
        if (hasPrefix)
        {
            return $"{_options.Prefix} ";
        }
        return string.Empty;
    }

    private LogLevel GetLogLevel(IMetricSnapshot snapshot)
    {
        if (_options.LogLevelSelector != null)
        {
            return _options.LogLevelSelector(snapshot);
        }

        if (HasFailure(snapshot))
        {
            return _options.FailureLogLevel;
        }

        return _options.LogLevel;
    }

    private static bool HasFailure(IMetricSnapshot snapshot) => snapshot switch
    {
        FailureSnapshot f => f.TotalFailures > 0,
        ThroughputSnapshot tp => tp.FailedOperations > 0,
        _ => false
    };

    private static string CleanMultiline(string? formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted)) return "(no data)";
        var lines = formatted.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" | ", lines);
    }
}
