using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// A real-time console dashboard sink that maintains an in-place multi-stage table using ANSI cursor repositioning
/// and periodic refresh intervals.
/// </summary>
public class LiveConsoleMetricSink : IMetricSink, IDisposable, IAsyncDisposable
{
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiBold = "\u001b[1m";
    private const string AnsiCyan = "\u001b[36m";
    private const string AnsiGray = "\u001b[90m";

    private readonly LiveConsoleMetricSinkOptions _options;
    private readonly ConcurrentDictionary<string, IMetricSnapshot> _latestSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Timer? _refreshTimer;
    private readonly object _renderLock = new();

    private int _lastRenderedLineCount;
    private bool _isDisposed;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets the options configuring this live dashboard sink.
    /// </summary>
    public LiveConsoleMetricSinkOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of <see cref="LiveConsoleMetricSink"/>.
    /// </summary>
    /// <param name="options">Optional dashboard options.</param>
    /// <param name="name">Optional sink name.</param>
    public LiveConsoleMetricSink(LiveConsoleMetricSinkOptions? options = null, string name = "LiveConsole")
    {
        _options = options ?? new LiveConsoleMetricSinkOptions();
        Name = string.IsNullOrWhiteSpace(name) ? "LiveConsole" : name;

        if (_options.RefreshInterval > TimeSpan.Zero)
        {
            _refreshTimer = new Timer(
                callback: _ => RenderDashboard(isFinal: false),
                state: null,
                dueTime: _options.RefreshInterval,
                period: _options.RefreshInterval
            );
        }
    }

    /// <inheritdoc />
    public void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
    {
        if (snapshots.Count == 0 || _isDisposed)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            var s = snapshots[i];
            var key = $"{s.MetricName}::{s.CounterName}";
            _latestSnapshots[key] = s;
        }
    }

    /// <inheritdoc />
    public ValueTask EmitAsync(IReadOnlyList<IMetricSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        Emit(snapshots);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Manually triggers a dashboard render.
    /// </summary>
    /// <param name="isFinal">True if this is the final render upon disposal; otherwise false.</param>
    public void RenderDashboard(bool isFinal = false)
    {
        lock (_renderLock)
        {
            if (_isDisposed && !isFinal)
            {
                return;
            }

            var snapshots = _latestSnapshots.Values.ToList();
            if (snapshots.Count == 0)
            {
                return;
            }

            var operations = snapshots.GetOperationSummaries();
            if (operations.Count == 0)
            {
                return;
            }

            var writer = _options.OutputWriter ?? System.Console.Out;
            bool isRedirected = _options.OutputWriter != null || System.Console.IsOutputRedirected;
            bool color = _options.Colorize;

            var sb = new StringBuilder();

            // Reposition cursor if updating an existing in-place dashboard in an interactive terminal
            if (!isRedirected && _lastRenderedLineCount > 0 && !isFinal)
            {
                sb.Append($"\u001b[{_lastRenderedLineCount}A\u001b[0J");
            }

            // Header Banner
            var now = DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var elapsed = _stopwatch.Elapsed;
            var elapsedStr = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

            if (color) sb.Append(AnsiCyan).Append(AnsiBold);
            sb.Append("=== ").Append(_options.Title).Append(" ===");
            if (color) sb.Append(AnsiReset);

            sb.Append(' ');
            if (color) sb.Append(AnsiGray);
            sb.Append('[').Append(now).Append("] ");
            sb.Append("(Elapsed: ").Append(elapsedStr).Append(", Operations: ").Append(operations.Count).Append(')');
            if (color) sb.Append(AnsiReset);
            sb.AppendLine();

            // Render Table
            var tableOpt = _options.TableOptions;
            tableOpt.Colorize = _options.Colorize;
            tableOpt.ShowDimensionBreakdown = _options.ShowDimensionBreakdown;
            tableOpt.MaxDimensionValuesToRender = _options.MaxDimensionValuesToRender;
            tableOpt.Prefix = string.Empty;
            tableOpt.IncludeTimestamp = false;

            var tableStr = TableMetricFormatter.RenderTable(operations, tableOpt);
            sb.AppendLine(tableStr);

            var fullText = sb.ToString();

            // Count lines rendered
            int lineCount = 0;
            for (int i = 0; i < fullText.Length; i++)
            {
                if (fullText[i] == '\n') lineCount++;
            }
            _lastRenderedLineCount = lineCount;

            writer.Write(fullText);
            writer.Flush();
        }
    }

    /// <summary>
    /// Flushes the final state and disposes background refresh timers.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously flushes the final state and disposes background refresh timers.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Releases managed resources used by the live dashboard.
    /// </summary>
    /// <param name="disposing">True if releasing managed resources; otherwise false.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (disposing)
        {
            _refreshTimer?.Dispose();
            RenderDashboard(isFinal: true);
        }
    }
}
