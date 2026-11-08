using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Extensions;
using DotnetKit.MetricFlow.Meters;
using DotnetKit.MetricFlow.Sinks;

namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Provides the abstract base implementation of <see cref="IMetricTracker"/>, managing topic tags,
/// counter registrations, sampling rates, System.Diagnostics.Metrics instrumentation bridges, and metric sinks.
/// </summary>
public abstract class MetricTrackerBase : IMetricTracker
{
    private readonly ConcurrentDictionary<string, ICounter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _counterRegistrationSequences = new(StringComparer.OrdinalIgnoreCase);
    private long _counterSequenceCounter;
    private volatile ICounter[] _activeCounters = [];

    private readonly ConcurrentDictionary<string, IMetricSink> _sinks = new(StringComparer.OrdinalIgnoreCase);
    private readonly MetricSinkTriggerOptions _sinkTriggers;
    private readonly ConcurrentDictionary<string, long> _metricExecutionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _operationSequences = new(StringComparer.OrdinalIgnoreCase);
    private long _operationSequenceCounter;

    private readonly AsyncLocal<Dictionary<string, Stack<InOperationState>>?> _asyncOperations = new();
    private readonly AsyncLocal<Dictionary<string, int>?> _asyncDroppedCounts = new();

    private readonly double? _samplingRate;
    private readonly IMetricMeterBridge? _meterBridge;
    private readonly object _periodicFlushLock = new();
    private CancellationTokenSource? _periodicFlushCts;
    private Task? _periodicFlushTask;
    private int _isFlushing;
    private int _disposed;

    /// <summary>
    /// Gets the topic name assigned to this metric tracker.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Gets the optional static tags associated with this tracker at the topic level.
    /// </summary>
    public Dictionary<string, string>? TopicTags { get; }

    /// <summary>
    /// Gets the <see cref="IMetricMeterBridge"/> used for System.Diagnostics.Metrics instrumentation, if configured.
    /// </summary>
    public IMetricMeterBridge? MeterBridge => _meterBridge;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTrackerBase"/> with the specified topic and optional configuration.
    /// </summary>
    /// <param name="topic">The topic name for this tracker.</param>
    /// <param name="topicTags">Optional static tags for the topic.</param>
    /// <param name="samplingRate">Sampling rate between 0.0 and 1.0 (or null to track 100%).</param>
    /// <param name="configObservable">Optional configuration observable for dynamic counter toggling.</param>
    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0,
        ICounterConfigObservable? configObservable = null)
        : this(topic, topicTags, samplingRate, configObservable, meterBridge: null, sinks: null, sinkTriggers: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTrackerBase"/> with an instrumentation meter bridge.
    /// </summary>
    /// <param name="topic">The topic name for this tracker.</param>
    /// <param name="topicTags">Optional static tags for the topic.</param>
    /// <param name="samplingRate">Sampling rate between 0.0 and 1.0 (or null to track 100%).</param>
    /// <param name="configObservable">Optional configuration observable for dynamic counter toggling.</param>
    /// <param name="meterBridge">Optional meter bridge for System.Diagnostics.Metrics instrumentation.</param>
    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IMetricMeterBridge? meterBridge)
        : this(topic, topicTags, samplingRate, configObservable, meterBridge, sinks: null, sinkTriggers: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricTrackerBase"/> with full configuration parameters.
    /// </summary>
    /// <param name="topic">The topic name for this tracker.</param>
    /// <param name="topicTags">Optional static tags for the topic.</param>
    /// <param name="samplingRate">Sampling rate between 0.0 and 1.0 (or null to track 100%).</param>
    /// <param name="configObservable">Optional configuration observable for dynamic counter toggling.</param>
    /// <param name="meterBridge">Optional meter bridge for System.Diagnostics.Metrics instrumentation.</param>
    /// <param name="sinks">Optional collection of metric sinks.</param>
    /// <param name="sinkTriggers">Optional execution lifecycle triggers for sink emission.</param>
    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IMetricMeterBridge? meterBridge,
        IEnumerable<IMetricSink>? sinks,
        MetricSinkTriggerOptions? sinkTriggers)
    {
        Topic = topic;
        TopicTags = topicTags;
        _samplingRate = samplingRate;
        _meterBridge = meterBridge ?? new MetricFlowMeterBridge(topic, topicTags, new MetricFlowMeterOptions());
        _sinkTriggers = sinkTriggers ?? new MetricSinkTriggerOptions();

        if (sinks != null)
        {
            foreach (var sink in sinks)
            {
                _sinks[sink.Name] = sink;
            }
        }

        configObservable?.Subscribe(OnCounterConfigChanged);
    }

    /// <summary>
    /// Registers a performance counter on this tracker.
    /// </summary>
    /// <param name="counter">The counter instance to register.</param>
    /// <returns>This tracker for fluent chaining.</returns>
    public IMetricTracker RegisterCounter(ICounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        _counters[counter.Name] = counter;
        _counterRegistrationSequences.GetOrAdd(
            counter.Name,
            _ => Interlocked.Increment(ref _counterSequenceCounter)
        );
        RebuildActiveCounters();
        return this;
    }

    /// <summary>
    /// Unregisters a performance counter by name.
    /// </summary>
    /// <param name="counterName">The name of the counter to unregister.</param>
    /// <returns><c>true</c> if the counter was removed; otherwise, <c>false</c>.</returns>
    public bool UnregisterCounter(string counterName)
    {
        var removed = _counters.TryRemove(counterName, out _);
        if (removed)
        {
            _counterRegistrationSequences.TryRemove(counterName, out _);
            RebuildActiveCounters();
        }
        return removed;
    }

    /// <summary>
    /// Enables or disables a specific registered counter.
    /// </summary>
    /// <param name="counterName">The name of the counter.</param>
    /// <param name="enabled">Whether the counter should be enabled.</param>
    public void SetCounterEnabled(string counterName, bool enabled)
    {
        if (_counters.TryGetValue(counterName, out var counter))
        {
            counter.IsEnabled = enabled;
            RebuildActiveCounters();
        }
    }

    /// <summary>
    /// Gets all registered counters on this tracker.
    /// </summary>
    /// <returns>An enumerable collection of registered counters.</returns>
    public IEnumerable<ICounter> GetCounters() =>
        _counters
            .Values.OrderBy(c =>
                _counterRegistrationSequences.TryGetValue(c.Name, out var seq) ? seq : long.MaxValue
            );

    /// <summary>
    /// Retrieves a registered counter by name.
    /// </summary>
    /// <param name="counterName">The name of the counter.</param>
    /// <returns>The counter instance, or <c>null</c> if not found.</returns>
    public ICounter? GetCounter(string counterName)
    {
        _counters.TryGetValue(counterName, out var counter);
        return counter;
    }

    /// <summary>
    /// Begins tracking an execution scope for the specified metric name.
    /// Returns an <see cref="IDisposable"/> scope that completes tracking upon disposal.
    /// </summary>
    /// <param name="metricName">The name of the metric operation.</param>
    /// <param name="tags">Optional tags associated with this execution.</param>
    /// <param name="metadata">Optional numeric metadata (e.g. byte size, item counts).</param>
    /// <returns>A disposable tracker scope.</returns>
    public IDisposable Track(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null)
    {
        if (ShouldDrop(_samplingRate))
        {
            return NoOpDisposable.Instance;
        }

        var active = _activeCounters;
        if (active.Length == 0 && (_meterBridge == null || !_meterBridge.IsEnabled) && _sinks.IsEmpty)
        {
            return NoOpDisposable.Instance;
        }

        Action<string, TimeSpan, bool, Exception?>? onCompleted = null;
        if (!_sinks.IsEmpty && _sinkTriggers.HasTriggers)
        {
            onCompleted = HandleOperationCompleted;
        }

        EnsureOperationSequence(metricName);
        return new CodeTracker(active, metricName, tags, metadata, _meterBridge, onCompleted);
    }

    /// <summary>
    /// Begins tracking an execution scope using the calling member name as the metric name.
    /// </summary>
    /// <param name="tags">Optional tags associated with this execution.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="metricName">Automatically populated with the caller member name.</param>
    /// <returns>A disposable tracker scope.</returns>
    public IDisposable Track(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "")
        => Track(metricName, tags, metadata);

    /// <summary>
    /// Records the entry of an operation for asynchronous or multi-step execution tracking.
    /// </summary>
    /// <param name="metricName">The name of the metric operation.</param>
    /// <param name="tags">Optional tags associated with this operation.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    public void In(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null)
    {
        if (ShouldDrop(_samplingRate))
        {
            RecordDropped(metricName);
            return;
        }

        EnsureOperationSequence(metricName);

        var active = _activeCounters;
        var states = new object?[active.Length];
        var inContext = new InContext(metricName, tags, metadata);

        for (var i = 0; i < active.Length; i++)
        {
            states[i] = active[i].OnIn(in inContext);
        }

        TagList? inFlightTags = null;
        if (_meterBridge is { IsEnabled: true })
        {
            inFlightTags = _meterBridge.RecordOperationIn(metricName, tags, metadata);
        }

        RecordInOperation(metricName, new InOperationState(Stopwatch.GetTimestamp(), active, states, inFlightTags));
    }

    /// <summary>
    /// Records the entry of an operation using the calling member name as the metric name.
    /// </summary>
    /// <param name="tags">Optional tags associated with this operation.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="metricName">Automatically populated with the caller member name.</param>
    public void In(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "")
        => In(metricName, tags, metadata);

    internal void HandleOperationCompleted(string metricName, TimeSpan duration, bool failed, Exception? exception)
    {
        if (!_sinks.IsEmpty && _sinkTriggers.HasTriggers)
        {
            var executionCount = _metricExecutionCounts.AddOrUpdate(metricName, 1, static (_, count) => count + 1);
            if (_sinkTriggers.ShouldTrigger(executionCount, failed, duration))
            {
                DispatchMetricSnapshotsToSinks(metricName);
            }
        }
    }

    /// <summary>
    /// Records the completion of an operation using the calling member name as the metric name.
    /// </summary>
    /// <param name="tags">Optional tags associated with this operation.</param>
    /// <param name="failed">Whether the operation failed logically.</param>
    /// <param name="exception">Optional exception that caused failure.</param>
    /// <param name="duration">Optional explicit duration; if null, elapsed time is computed automatically.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="metricName">Automatically populated with the caller member name.</param>
    public void Out(
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null,
        [CallerMemberName] string metricName = "")
        => Out(metricName, tags, failed, exception, duration, metadata);

    /// <summary>
    /// Records the completion of an operation with outcome, failure status, and optional duration.
    /// </summary>
    /// <param name="metricName">The name of the metric operation.</param>
    /// <param name="tags">Optional tags associated with this operation.</param>
    /// <param name="failed">Whether the operation failed logically.</param>
    /// <param name="exception">Optional exception that caused failure.</param>
    /// <param name="duration">Optional explicit duration; if null, elapsed time is computed automatically.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    public void Out(
        string metricName,
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null)
    {
        if (TryConsumeDropped(metricName))
        {
            return;
        }

        var opState = TryPopInOperation(metricName);

        if (!duration.HasValue && opState != null)
        {
            duration = Stopwatch.GetElapsedTime(opState.StartTimestamp);
        }

        if (opState?.InFlightTags.HasValue == true)
        {
            _meterBridge?.RecordOperationInFlightEnd(metricName, opState.InFlightTags.Value);
        }

        var outContext = new OutContext(metricName, failed, exception, duration, tags, metadata);

        if (opState != null)
        {
            for (var i = 0; i < opState.Counters.Length; i++)
            {
                if (opState.Counters[i].IsEnabled)
                {
                    opState.Counters[i].OnOut(opState.States[i], in outContext);
                }
            }
        }
        else
        {
            EnsureOperationSequence(metricName);
            // Fallback: If Out was called without a preceding In on this async context
            var active = _activeCounters;
            foreach (var t in active)
            {
                if (t.IsEnabled)
                {
                    t.OnOut(null, in outContext);
                }
            }
        }

        if (_meterBridge is { IsEnabled: true })
        {
            var opDuration = duration ?? TimeSpan.Zero;
            _meterBridge.RecordOperationOut(metricName, opDuration, failed, exception, tags, metadata);
        }

        HandleOperationCompleted(metricName, duration ?? TimeSpan.Zero, failed, exception);
    }

    /// <summary>
    /// Gets a specific counter snapshot for the given metric and counter name.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="counterName">The name of the counter.</param>
    /// <returns>The metric snapshot, or <c>null</c> if not found.</returns>
    public IMetricSnapshot? GetSnapshot(string metricName, string counterName)
    {
        return _counters.TryGetValue(counterName, out var counter) ? counter.GetSnapshot(metricName) : null;
    }

    /// <summary>
    /// Gets all counter snapshots for a specific metric.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    /// <returns>An enumerable collection of snapshots for this metric.</returns>
    public IEnumerable<IMetricSnapshot> GetSnapshots(string metricName)
    {
        foreach (var counter in GetCounters())
        {
            var snapshot = counter.GetSnapshot(metricName);
            if (snapshot != null)
            {
                yield return snapshot;
            }
        }
    }

    /// <summary>
    /// Gets all snapshots across all registered counters and metrics, ordered by the sequence in which operations were first tracked.
    /// </summary>
    /// <returns>An enumerable collection of all current snapshots.</returns>
    public IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        var snapshots = new List<IMetricSnapshot>();
        var counterOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var counterIdx = 0;

        foreach (var counter in GetCounters())
        {
            counterOrder[counter.Name] = counterIdx++;
            foreach (var snapshot in counter.GetAllSnapshots())
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots
            .OrderBy(s =>
                _operationSequences.TryGetValue(s.MetricName, out var seq) ? seq : long.MaxValue
            )
            .ThenBy(s =>
                counterOrder.TryGetValue(s.CounterName, out var cIdx) ? cIdx : int.MaxValue
            );
    }

    /// <summary>
    /// Registers a metric sink to receive snapshot emissions from this tracker.
    /// </summary>
    /// <param name="sink">The metric sink instance to register.</param>
    /// <returns>This tracker for fluent chaining.</returns>
    public IMetricTracker RegisterSink(IMetricSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sinks[sink.Name] = sink;
        return this;
    }

    /// <summary>
    /// Unregisters a metric sink by name.
    /// </summary>
    /// <param name="sinkName">The name of the sink to unregister.</param>
    /// <returns><c>true</c> if the sink was removed; otherwise, <c>false</c>.</returns>
    public bool UnregisterSink(string sinkName)
    {
        return _sinks.TryRemove(sinkName, out _);
    }

    /// <summary>
    /// Gets all registered metric sinks.
    /// </summary>
    /// <returns>An enumerable collection of registered sinks.</returns>
    public IEnumerable<IMetricSink> GetSinks() => _sinks.Values;

    /// <summary>
    /// Gets the execution lifecycle triggers configured for this tracker's sinks.
    /// </summary>
    public MetricSinkTriggerOptions SinkTriggers => _sinkTriggers;

    /// <summary>
    /// Gets or sets whether registered sinks should be automatically flushed when this tracker is disposed.
    /// </summary>
    public bool AutoFlushOnDispose { get; set; }

    /// <summary>
    /// Gets the configured periodic background flush interval, if active.
    /// </summary>
    public TimeSpan? PeriodicFlushInterval { get; private set; }

    /// <summary>
    /// Starts or updates an asynchronous background periodic timer that calls <see cref="FlushSinksAsync"/> at the specified interval.
    /// Automatically stopped when <see cref="StopPeriodicFlush"/> or <see cref="Dispose()"/> / <see cref="DisposeAsync()"/> is called.
    /// </summary>
    /// <param name="interval">The time interval between periodic flushes.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public IMetricTracker StartPeriodicFlush(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }

        lock (_periodicFlushLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return this;
            }

            StopPeriodicFlushCore();
            PeriodicFlushInterval = interval;
            var cts = new CancellationTokenSource();
            _periodicFlushCts = cts;
            _periodicFlushTask = RunPeriodicFlushLoopAsync(interval, cts.Token);
        }

        return this;
    }

    /// <summary>
    /// Stops any active background periodic flush loop on this tracker.
    /// </summary>
    public void StopPeriodicFlush()
    {
        lock (_periodicFlushLock)
        {
            StopPeriodicFlushCore();
        }
    }

    private void StopPeriodicFlushCore()
    {
        PeriodicFlushInterval = null;
        if (_periodicFlushCts != null)
        {
            try
            {
                _periodicFlushCts.Cancel();
                _periodicFlushCts.Dispose();
            }
            catch
            {
                // Ignore cancellation disposal errors
            }
            _periodicFlushCts = null;
            _periodicFlushTask = null;
        }
    }

    private async Task RunPeriodicFlushLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
                {
                    continue;
                }

                try
                {
                    await FlushSinksAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Sinks must not crash the periodic timer loop
                }
                finally
                {
                    Interlocked.Exchange(ref _isFlushing, 0);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation / disposal
        }
        catch
        {
            // Prevent background task fault
        }
    }

    /// <summary>
    /// Synchronously flushes all active snapshots across all registered counters to all registered sinks.
    /// </summary>
    public void FlushSinks()
    {
        if (_sinks.IsEmpty) return;
        var allSnapshots = GetAllSnapshots().ToList();
        if (allSnapshots.Count == 0) return;

        foreach (var sink in _sinks.Values)
        {
            try
            {
                sink.Emit(allSnapshots);
            }
            catch
            {
                // Sinks must not crash the caller
            }
        }
    }

    /// <summary>
    /// Asynchronously flushes all active snapshots across all registered counters to all registered sinks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous flush operation.</returns>
    public async ValueTask FlushSinksAsync(CancellationToken cancellationToken = default)
    {
        if (_sinks.IsEmpty) return;
        var allSnapshots = GetAllSnapshots().ToList();
        if (allSnapshots.Count == 0) return;

        foreach (var sink in _sinks.Values)
        {
            try
            {
                await sink.EmitAsync(allSnapshots, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Sinks must not crash the caller
            }
        }
    }

    private void DispatchMetricSnapshotsToSinks(string metricName)
    {
        var snapshots = GetSnapshots(metricName).ToList();
        if (snapshots.Count == 0) return;

        foreach (var sink in _sinks.Values)
        {
            try
            {
                sink.Emit(snapshots);
            }
            catch
            {
                // Sinks must not crash the tracking operation
            }
        }
    }

    /// <summary>
    /// Resets all registered counters, execution counts, and meter bridges.
    /// </summary>
    public void Clear()
    {
        foreach (var counter in _counters.Values)
        {
            counter.Reset();
        }
        _metricExecutionCounts.Clear();
        _operationSequences.Clear();
        Interlocked.Exchange(ref _operationSequenceCounter, 0);
        _meterBridge?.Reset();
    }

    /// <summary>
    /// Disposes the tracker, optionally flushing registered sinks if <see cref="AutoFlushOnDispose"/> is enabled,
    /// and disposing all registered disposable sinks and instrumentation bridges.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the tracker, awaiting <see cref="FlushSinksAsync"/> if <see cref="AutoFlushOnDispose"/> is enabled,
    /// and asynchronously disposing all registered sinks and instrumentation bridges.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs asynchronous cleanup of managed resources, including stopping periodic flushes,
    /// flushing sinks if <see cref="AutoFlushOnDispose"/> is enabled, and disposing sinks.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous cleanup operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Task? pendingFlushTask;
        lock (_periodicFlushLock)
        {
            pendingFlushTask = _periodicFlushTask;
            StopPeriodicFlushCore();
        }

        if (pendingFlushTask != null)
        {
            try
            {
                await pendingFlushTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore background task cancellation/completion exceptions
            }
        }

        if (AutoFlushOnDispose)
        {
            try
            {
                await FlushSinksAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Sinks should not prevent disposal
            }
        }

        _meterBridge?.Dispose();
        foreach (var sink in _sinks.Values)
        {
            try
            {
                if (sink is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (sink is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Sinks should not prevent other resources from disposing
            }
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MetricTrackerBase"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        StopPeriodicFlush();

        if (AutoFlushOnDispose)
        {
            try
            {
                FlushSinks();
            }
            catch
            {
                // Sinks should not prevent disposal
            }
        }

        _meterBridge?.Dispose();
        foreach (var sink in _sinks.Values)
        {
            if (sink is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Sinks should not prevent other resources from disposing
                }
            }
        }
    }

    /// <summary>
    /// Formats all active snapshots and topic tags into a human-readable diagnostic string.
    /// </summary>
    /// <returns>A string representation of the current tracker state.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Topic);
        if (TopicTags != null)
        {
            var tagsDict = new Dictionary<string, string>(TopicTags);
            sb.AppendLine(tagsDict.ToFormattedString("Topic Tags"));
        }

        sb.Append(GetAllSnapshots().ToFormattedString());
        return sb.ToString();
    }

    private void OnCounterConfigChanged(string counterName, bool enabled)
    {
        SetCounterEnabled(counterName, enabled);
    }

    private void RebuildActiveCounters()
    {
        _activeCounters = [.. GetCounters().Where(c => c.IsEnabled)];
    }

    private void EnsureOperationSequence(string metricName)
    {
        if (!_operationSequences.ContainsKey(metricName))
        {
            _operationSequences.GetOrAdd(
                metricName,
                _ => Interlocked.Increment(ref _operationSequenceCounter)
            );
        }
    }

    private static bool ShouldDrop(double? rate)
    {
        if (!rate.HasValue || rate.Value >= 1.0)
        {
            return false;
        }
        if (rate.Value <= 0.0)
        {
            return true;
        }
        return Random.Shared.NextDouble() > rate.Value;
    }

    private void RecordInOperation(string metricName, InOperationState opState)
    {
        var dict = _asyncOperations.Value;
        if (dict == null)
        {
            dict = new Dictionary<string, Stack<InOperationState>>();
            _asyncOperations.Value = dict;
        }

        if (!dict.TryGetValue(metricName, out var stack))
        {
            stack = new Stack<InOperationState>();
            dict[metricName] = stack;
        }

        stack.Push(opState);
    }

    private InOperationState? TryPopInOperation(string metricName)
    {
        var dict = _asyncOperations.Value;
        if (dict != null && dict.TryGetValue(metricName, out var stack) && stack.Count > 0)
        {
            return stack.Pop();
        }
        return null;
    }

    private void RecordDropped(string metricName)
    {
        var dict = _asyncDroppedCounts.Value;
        if (dict == null)
        {
            dict = new Dictionary<string, int>();
            _asyncDroppedCounts.Value = dict;
        }

        dict[metricName] = dict.TryGetValue(metricName, out var count) ? count + 1 : 1;
    }

    private bool TryConsumeDropped(string metricName)
    {
        var dict = _asyncDroppedCounts.Value;
        if (dict != null && dict.TryGetValue(metricName, out var count) && count > 0)
        {
            if (count == 1)
            {
                dict.Remove(metricName);
            }
            else
            {
                dict[metricName] = count - 1;
            }
            return true;
        }

        return false;
    }

    private sealed class InOperationState(long startTimestamp, ICounter[] counters, object?[] states, TagList? inFlightTags)
    {
        public long StartTimestamp => startTimestamp;
        public ICounter[] Counters => counters;
        public object?[] States => states;
        public TagList? InFlightTags => inFlightTags;
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
