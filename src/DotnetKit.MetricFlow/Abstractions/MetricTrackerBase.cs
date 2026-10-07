using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Extensions;
using DotnetKit.MetricFlow.Meters;
using DotnetKit.MetricFlow.Sinks;

namespace DotnetKit.MetricFlow.Abstractions;

public abstract class MetricTrackerBase : IMetricTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, ICounter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private volatile ICounter[] _activeCounters = [];

    private readonly ConcurrentDictionary<string, IMetricSink> _sinks = new(StringComparer.OrdinalIgnoreCase);
    private readonly MetricSinkTriggerOptions _sinkTriggers;
    private readonly ConcurrentDictionary<string, long> _metricExecutionCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly AsyncLocal<Dictionary<string, Stack<InOperationState>>?> _asyncOperations = new();
    private readonly AsyncLocal<Dictionary<string, int>?> _asyncDroppedCounts = new();

    private readonly string _topic;
    private readonly Dictionary<string, string>? _topicTags;
    private readonly double? _samplingRate;
    private readonly IMetricMeterBridge? _meterBridge;

    public string Topic => _topic;
    public Dictionary<string, string>? TopicTags => _topicTags;
    public IMetricMeterBridge? MeterBridge => _meterBridge;

    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0,
        ICounterConfigObservable? configObservable = null)
        : this(topic, topicTags, samplingRate, configObservable, meterBridge: null, sinks: null, sinkTriggers: null)
    {
    }

    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IMetricMeterBridge? meterBridge)
        : this(topic, topicTags, samplingRate, configObservable, meterBridge, sinks: null, sinkTriggers: null)
    {
    }

    protected MetricTrackerBase(
        string topic,
        Dictionary<string, string>? topicTags,
        double? samplingRate,
        ICounterConfigObservable? configObservable,
        IMetricMeterBridge? meterBridge,
        IEnumerable<IMetricSink>? sinks,
        MetricSinkTriggerOptions? sinkTriggers)
    {
        _topic = topic;
        _topicTags = topicTags;
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


    public IMetricTracker RegisterCounter(ICounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        _counters[counter.Name] = counter;
        RebuildActiveCounters();
        return this;
    }

    public bool UnregisterCounter(string counterName)
    {
        var removed = _counters.TryRemove(counterName, out _);
        if (removed)
        {
            RebuildActiveCounters();
        }
        return removed;
    }

    public void SetCounterEnabled(string counterName, bool enabled)
    {
        if (_counters.TryGetValue(counterName, out var counter))
        {
            counter.IsEnabled = enabled;
            RebuildActiveCounters();
        }
    }

    public IEnumerable<ICounter> GetCounters() => _counters.Values;

    public ICounter? GetCounter(string counterName)
    {
        _counters.TryGetValue(counterName, out var counter);
        return counter;
    }

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

        return new CodeTracker(active, metricName, tags, metadata, _meterBridge, onCompleted);
    }

    public IDisposable Track(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "")
        => Track(metricName, tags, metadata);

    public void In(string metricName, Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null)
    {
        if (ShouldDrop(_samplingRate))
        {
            RecordDropped(metricName);
            return;
        }

        var active = _activeCounters;
        var states = new object?[active.Length];
        var inContext = new InContext(metricName, tags, metadata);

        for (int i = 0; i < active.Length; i++)
        {
            states[i] = active[i].OnIn(in inContext);
        }

        TagList? inFlightTags = null;
        if (_meterBridge != null && _meterBridge.IsEnabled)
        {
            inFlightTags = _meterBridge.RecordOperationIn(metricName, tags, metadata);
        }

        RecordInOperation(metricName, new InOperationState(Stopwatch.GetTimestamp(), active, states, inFlightTags));
    }

    public void In(Dictionary<string, string>? tags = null, Dictionary<string, long>? metadata = null, [CallerMemberName] string metricName = "")
        => In(metricName, tags, metadata);

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

        InOperationState? opState = TryPopInOperation(metricName);

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
            for (int i = 0; i < opState.Counters.Length; i++)
            {
                if (opState.Counters[i].IsEnabled)
                {
                    opState.Counters[i].OnOut(opState.States[i], in outContext);
                }
            }
        }
        else
        {
            // Fallback: If Out was called without a preceding In on this async context
            var active = _activeCounters;
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i].IsEnabled)
                {
                    active[i].OnOut(null, in outContext);
                }
            }
        }

        if (_meterBridge != null && _meterBridge.IsEnabled)
        {
            var opDuration = duration ?? TimeSpan.Zero;
            _meterBridge.RecordOperationOut(metricName, opDuration, failed, exception, tags, metadata);
        }

        HandleOperationCompleted(metricName, duration ?? TimeSpan.Zero, failed, exception);
    }

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


    public void Out(
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        Dictionary<string, long>? metadata = null,
        [CallerMemberName] string metricName = "")
        => Out(metricName, tags, failed, exception, duration, metadata);

    public IMetricSnapshot? GetSnapshot(string metricName, string counterName)
    {
        return _counters.TryGetValue(counterName, out var counter) ? counter.GetSnapshot(metricName) : null;
    }

    public IEnumerable<IMetricSnapshot> GetSnapshots(string metricName)
    {
        foreach (var counter in _counters.Values)
        {
            var snapshot = counter.GetSnapshot(metricName);
            if (snapshot != null)
            {
                yield return snapshot;
            }
        }
    }

    public IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var counter in _counters.Values)
        {
            foreach (var snapshot in counter.GetAllSnapshots())
            {
                yield return snapshot;
            }
        }
    }

    public IMetricTracker RegisterSink(IMetricSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sinks[sink.Name] = sink;
        return this;
    }

    public bool UnregisterSink(string sinkName)
    {
        return _sinks.TryRemove(sinkName, out _);
    }

    public IEnumerable<IMetricSink> GetSinks() => _sinks.Values;

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

    public void Clear()
    {
        foreach (var counter in _counters.Values)
        {
            counter.Reset();
        }
        _metricExecutionCounts.Clear();
        _meterBridge?.Reset();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _meterBridge?.Dispose();
            foreach (var sink in _sinks.Values)
            {
                if (sink is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception)
                    {
                        // Sinks should not prevent other resources from disposing
                    }
                }
            }

        }
    }


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
        _activeCounters = _counters.Values.Where(c => c.IsEnabled).ToArray();
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