using System.Diagnostics;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Meters;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Tracks the execution lifecycle of a code block or operation, notifying registered counters and meter bridges upon disposal.
/// </summary>
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

    /// <summary>
    /// Gets the name of the metric or operation being tracked.
    /// </summary>
    public string MetricName => _metricName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeTracker"/> class.
    /// </summary>
    /// <param name="counters">The active counters to notify.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    public CodeTracker(
        ICounter[] counters,
        string metricName,
        Dictionary<string, string>? tags = null,
        Dictionary<string, long>? metadata = null)
        : this(counters, metricName, tags, metadata, meterBridge: null, onCompleted: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeTracker"/> class with meter bridge support.
    /// </summary>
    /// <param name="counters">The active counters to notify.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="meterBridge">Optional meter bridge connecting to System.Diagnostics.Metrics.</param>
    public CodeTracker(
        ICounter[] counters,
        string metricName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata,
        IMetricMeterBridge? meterBridge)
        : this(counters, metricName, tags, metadata, meterBridge, onCompleted: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeTracker"/> class with meter bridge and completion callback support.
    /// </summary>
    /// <param name="counters">The active counters to notify.</param>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="meterBridge">Optional meter bridge connecting to System.Diagnostics.Metrics.</param>
    /// <param name="onCompleted">Optional callback invoked upon operation completion.</param>
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

    /// <summary>
    /// Marks the tracked operation as failed or successful.
    /// </summary>
    /// <param name="failed"><c>true</c> to mark as failed; <c>false</c> otherwise.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetFailed(bool failed = true)
    {
        _failed = failed;
        return this;
    }

    /// <summary>
    /// Associates an exception with the tracked operation and marks it as failed.
    /// </summary>
    /// <param name="exception">The exception thrown during the operation.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetException(Exception? exception)
    {
        _exception = exception;
        if (exception != null)
        {
            _failed = true;
        }
        return this;
    }

    /// <summary>
    /// Adds or updates a string tag for the tracked operation.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetTag(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _tags ??= new Dictionary<string, string>();
        _tags[key] = value;
        return this;
    }

    /// <summary>
    /// Adds or updates a numeric metadata value for the tracked operation.
    /// </summary>
    /// <param name="key">The metadata key (e.g. "items", "bytes").</param>
    /// <param name="value">The numeric value.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetMetadata(string key, long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata ??= new Dictionary<string, long>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Convenience helper to record the number of items processed by this operation.
    /// </summary>
    /// <param name="count">The item count.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetItems(long count) => SetMetadata("items", count);

    /// <summary>
    /// Alias for <see cref="SetItems(long)"/>.
    /// </summary>
    /// <param name="count">The item count.</param>
    /// <returns>This tracker instance for fluent chaining.</returns>
    public CodeTracker SetItemCount(long count) => SetItems(count);

    /// <summary>
    /// Disposes the tracking scope and records exit metrics across all counters and bridges.
    /// </summary>
    /// <param name="disposing"><c>true</c> if disposing managed resources; otherwise, <c>false</c>.</param>
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

    /// <summary>
    /// Completes and disposes the tracked operation scope, recording elapsed duration and outcome metrics.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}