using System.Collections.Concurrent;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Periodically harvests metric snapshots from an <see cref="IMetricSnapshotsSource"/>,
/// computes interval deltas, and dispatches <see cref="MetricTimelineEntry"/> batches to registered <see cref="IMetricSink"/> instances.
/// </summary>
public sealed class PeriodicMetricExporter : IAsyncDisposable, IDisposable
{
    private readonly IMetricSnapshotsSource _snapshotsSource;
    private readonly IReadOnlyList<IMetricSink> _sinks;
    private readonly PeriodicMetricExporterOptions _options;
    private readonly ConcurrentDictionary<(string MetricName, string CounterName), IMetricSnapshot> _lastSnapshots = new();

    private DateTimeOffset? _lastHarvestTime;
    private PeriodicTimer? _timer;
    private Task? _timerTask;
    private CancellationTokenSource? _cts;
    private int _isRunning;
    private int _disposed;

    public PeriodicMetricExporter(
        IMetricSnapshotsSource snapshotsSource,
        IEnumerable<IMetricSink> sinks,
        PeriodicMetricExporterOptions? options = null)
    {
        _snapshotsSource = snapshotsSource ?? throw new ArgumentNullException(nameof(snapshotsSource));
        _sinks = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToList();
        _options = options ?? new PeriodicMetricExporterOptions();
    }

    /// <summary>
    /// Starts the background periodic harvesting loop.
    /// </summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
        {
            return; // Already running
        }

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(_options.Interval);
        _lastHarvestTime = DateTimeOffset.UtcNow;
        _timerTask = Task.Run(() => HarvestLoopAsync(_timer, _cts.Token));
    }

    private async Task HarvestLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await HarvestAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _options.OnError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Executes a single harvest cycle immediately, computing deltas and dispatching to sinks.
    /// </summary>
    public async ValueTask<IReadOnlyList<MetricTimelineEntry>> HarvestAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = _lastHarvestTime ?? now.Subtract(_options.Interval);
        _lastHarvestTime = now;

        var entries = new List<MetricTimelineEntry>();

        try
        {
            var currentSnapshots = _snapshotsSource.GetAllSnapshots().ToList();

            foreach (var current in currentSnapshots)
            {
                var key = (current.MetricName, current.CounterName);
                _lastSnapshots.TryGetValue(key, out var previous);

                var delta = current.ComputeDelta(previous);
                _lastSnapshots[key] = current;

                if (!_options.IncludeZeroDeltaEntries && !HasActivity(delta))
                {
                    continue;
                }

                entries.Add(new MetricTimelineEntry(
                    PeriodStart: periodStart,
                    PeriodEnd: now,
                    Cumulative: current,
                    Delta: delta,
                    Resource: _options.Resource,
                    Tags: _options.GlobalTags
                ));
            }

            if (entries.Count > 0 && _sinks.Count > 0)
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        await sink.EmitAsync(entries, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _options.OnError?.Invoke(ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _options.OnError?.Invoke(ex);
        }

        return entries;
    }

    private static bool HasActivity(IMetricSnapshot delta)
    {
        return delta switch
        {
            DurationSnapshot d => d.OutCount > 0 || d.InCount > 0,
            MemorySnapshot m => m.OperationCount > 0,
            ExceptionSnapshot e => e.TotalOperations > 0 || e.TotalFailures > 0,
            _ => true
        };
    }

    /// <summary>
    /// Stops the periodic harvesting loop and optionally flushes remaining snapshots.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
            return;
        }

        if (_cts != null)
        {
            _cts.Cancel();
            if (_timerTask != null)
            {
                try
                {
                    await _timerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }
            _cts.Dispose();
            _cts = null;
        }

        _timer?.Dispose();
        _timer = null;

        // Perform final flush
        await HarvestAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        Interlocked.Exchange(ref _isRunning, 0);

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
