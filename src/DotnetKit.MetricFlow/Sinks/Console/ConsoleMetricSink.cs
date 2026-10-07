using System.Text;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

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
    /// Formats a single metric snapshot into a log line according to configured options.
    /// </summary>
    /// <param name="snapshot">The snapshot to format.</param>
    /// <returns>A formatted string line.</returns>
    public string FormatSnapshot(IMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder(128);
        bool color = _options.Colorize;

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
        if (color) sb.Append(AnsiBold).Append(AnsiYellow);
        sb.Append('[').Append(snapshot.CounterName).Append(':').Append(snapshot.MetricName).Append(']');
        if (color) sb.Append(AnsiReset);

        sb.Append(' ');

        // Details based on snapshot type
        AppendSnapshotDetails(sb, snapshot, color);

        return sb.ToString();
    }

    private static void AppendSnapshotDetails(StringBuilder sb, IMetricSnapshot snapshot, bool color)
    {
        switch (snapshot)
        {
            case ThroughputSnapshot tp:
                sb.Append("TotalItems: ").Append(tp.TotalItems)
                  .Append(", Operations: ").Append(tp.TotalOperations)
                  .Append(", Rate: ").Append(tp.ItemsPerSecond.ToString("F1")).Append(" items/s");

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
                    if (color) sb.Append(AnsiRed);
                    sb.Append(f.TotalFailures).Append(" (").Append((f.FailureRate * 100).ToString("F1")).Append("%)");
                    if (color) sb.Append(AnsiReset);
                }
                else
                {
                    if (color) sb.Append(AnsiGreen);
                    sb.Append("0 (0.0%)");
                    if (color) sb.Append(AnsiReset);
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
                    sb.Append(string.Join(" | ", lines));
                }
                break;
        }
    }
}
