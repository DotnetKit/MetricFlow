using System.Diagnostics;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Meters;

namespace DotnetKit.MetricFlow;

public class CodeTracker : IDisposable
{
    private readonly ICounter[] _counters;
    private readonly object?[] _states;
    private readonly string _metricName;
    private Dictionary<string, string>? _tags;
    private Dictionary<string, long>? _metadata;
    private readonly long _startTimestamp;
    private readonly IMetricMeterBridge? _meterBridge;
    private readonly TagList? _inFlightTagList;
    private readonly Action<string, TimeSpan, bool, Exception?>? _onCompleted;

    private bool _failed;
    private Exception? _exception;
    private int _disposed;

    public string MetricName => _metricName;

    public CodeTracker(
        ICounter[] counters,
        string metricName,
        Dictionary<string, string>? tags = null,
        Dictionary<string, long>? metadata = null)
        : this(counters, metricName, tags, metadata, meterBridge: null, onCompleted: null)
    {
    }

    public CodeTracker(
        ICounter[] counters,
        string metricName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata,
        IMetricMeterBridge? meterBridge)
        : this(counters, metricName, tags, metadata, meterBridge, onCompleted: null)
    {
    }

    public CodeTracker(
        ICounter[] counters,
        string metricName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata,
        IMetricMeterBridge? meterBridge,
        Action<string, TimeSpan, bool, Exception?>? onCompleted)
    {
        _counters = counters;
        _metricName = metricName;
        _tags = tags != null ? new Dictionary<string, string>(tags) : null;
        _metadata = metadata != null ? new Dictionary<string, long>(metadata) : null;
        _startTimestamp = Stopwatch.GetTimestamp();
        _states = new object?[counters.Length];
        _meterBridge = meterBridge;
        _onCompleted = onCompleted;


        var inContext = new InContext(metricName, _tags, _metadata);
        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i].IsEnabled)
            {
                _states[i] = counters[i].OnIn(in inContext);
            }
        }

        if (_meterBridge != null && _meterBridge.IsEnabled)
        {
            _inFlightTagList = _meterBridge.RecordOperationIn(_metricName, _tags, _metadata);
        }
    }

    public CodeTracker SetFailed(bool failed = true)
    {
        _failed = failed;
        return this;
    }

    public CodeTracker SetException(Exception? exception)
    {
        _exception = exception;
        if (exception != null)
        {
            _failed = true;
        }
        return this;
    }

    public CodeTracker SetTag(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _tags ??= new Dictionary<string, string>();
        _tags[key] = value;
        return this;
    }

    public CodeTracker SetMetadata(string key, long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata ??= new Dictionary<string, long>();
        _metadata[key] = value;
        return this;
    }

    public CodeTracker SetItems(long count) => SetMetadata("items", count);

    public CodeTracker SetItemCount(long count) => SetItems(count);

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            if (_inFlightTagList.HasValue)
            {
                _meterBridge?.RecordOperationInFlightEnd(_metricName, _inFlightTagList.Value);
            }

            var duration = Stopwatch.GetElapsedTime(_startTimestamp);
            var outContext = new OutContext(_metricName, _failed, _exception, duration, _tags, _metadata);

            for (int i = 0; i < _counters.Length; i++)
            {
                if (_counters[i].IsEnabled)
                {
                    _counters[i].OnOut(_states[i], in outContext);
                }
            }

            _meterBridge?.RecordOperationOut(_metricName, duration, _failed, _exception, _tags, _metadata);
            _onCompleted?.Invoke(_metricName, duration, _failed, _exception);
        }
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}