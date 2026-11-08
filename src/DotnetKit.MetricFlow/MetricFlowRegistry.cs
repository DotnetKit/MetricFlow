using System.Collections.Concurrent;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Sinks;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Central registry managing all metric trackers across topics.
/// </summary>
public class MetricFlowRegistry : IMetricFlow
{
    private readonly ConcurrentDictionary<string, IMetricTracker> _trackers = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly List<IMetricSink> _sharedSinks = [];
    private readonly object _sinksLock = new();
    private readonly Func<string, IMetricTracker>? _trackerFactory;
    private readonly string _defaultTopic;
    private volatile IMetricTracker? _defaultTracker;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricFlowRegistry"/>.
    /// </summary>
    /// <param name="defaultTopic">The fallback topic name when no tracker is registered.</param>
    /// <param name="initialTrackers">Optional initial trackers to register.</param>
    /// <param name="trackerFactory">Optional factory to produce trackers dynamically for unconfigured topics.</param>
    /// <param name="sharedSinks">Optional shared sinks owned by this registry.</param>
    public MetricFlowRegistry(
        string defaultTopic = "Application",
        IEnumerable<IMetricTracker>? initialTrackers = null,
        Func<string, IMetricTracker>? trackerFactory = null,
        IEnumerable<IMetricSink>? sharedSinks = null
    )
    {
        _defaultTopic = string.IsNullOrWhiteSpace(defaultTopic) ? "Application" : defaultTopic;
        _trackerFactory = trackerFactory;

        if (sharedSinks != null)
        {
            foreach (var sink in sharedSinks)
            {
                RegisterSharedSink(sink);
            }
        }

        if (initialTrackers != null)
        {
            foreach (var tracker in initialTrackers)
            {
                RegisterTracker(tracker);
            }
        }
    }

    /// <summary>
    /// Registers a shared sink owned by this registry.
    /// </summary>
    /// <param name="sink">The shared metric sink instance.</param>
    public void RegisterSharedSink(IMetricSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_sinksLock)
        {
            if (!_sharedSinks.Contains(sink))
            {
                _sharedSinks.Add(sink);
            }
        }
    }

    /// <summary>
    /// Gets all shared metric sinks owned by this registry.
    /// </summary>
    public IReadOnlyList<IMetricSink> SharedSinks
    {
        get
        {
            lock (_sinksLock)
            {
                return _sharedSinks.ToArray();
            }
        }
    }

    /// <inheritdoc />
    IEnumerable<IMetricSink> IMetricFlow.SharedSinks => SharedSinks;

    /// <inheritdoc />
    public IMetricTracker? DefaultTracker
    {
        get
        {
            if (_defaultTracker != null)
            {
                return _defaultTracker;
            }

            if (_trackers.TryGetValue(_defaultTopic, out var found))
            {
                _defaultTracker = found;
                return _defaultTracker;
            }

            _defaultTracker = GetTracker(_defaultTopic);
            return _defaultTracker;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IMetricTracker> Trackers => _trackers.Values;

    /// <inheritdoc />
    public IMetricTracker GetTracker(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        return _trackers.GetOrAdd(
            topic,
            key =>
            {
                IMetricTracker factoryTracker;
                if (_trackerFactory != null)
                {
                    factoryTracker = _trackerFactory(key);
                }
                else
                {
                    factoryTracker = new MetricTracker(key);
                }

                foreach (var sink in factoryTracker.GetSinks())
                {
                    RegisterSharedSink(sink);
                }
                factoryTracker.DisposeSinks = false;

                // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
                if (_defaultTracker == null)
                {
                    _defaultTracker = factoryTracker;
                }
                return factoryTracker;
            }
        );
    }

    /// <inheritdoc />
    public bool TryGetTracker(string topic, out IMetricTracker? tracker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return _trackers.TryGetValue(topic, out tracker);
    }

    /// <summary>
    /// Registers a tracker instance in the registry.
    /// </summary>
    /// <param name="tracker">The tracker to register.</param>
    /// <param name="setAsDefault">Whether to set this tracker as the default.</param>
    public void RegisterTracker(IMetricTracker tracker, bool setAsDefault = false)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _trackers[tracker.Topic] = tracker;

        if (setAsDefault || _defaultTracker == null)
        {
            _defaultTracker = tracker;
        }

        foreach (var sink in tracker.GetSinks())
        {
            RegisterSharedSink(sink);
        }
        tracker.DisposeSinks = false;
    }

    /// <summary>
    /// Synchronously flushes all active snapshots across all registered topic trackers to their respective sinks.
    /// </summary>
    public void FlushAllSinks()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.FlushSinks();
        }
    }

    /// <summary>
    /// Asynchronously flushes all active snapshots across all registered topic trackers to their respective sinks.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous flush operation.</returns>
    public async ValueTask FlushAllSinksAsync(CancellationToken cancellationToken = default)
    {
        foreach (var tracker in _trackers.Values)
        {
            await tracker.FlushSinksAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes all registered metric trackers that implement IDisposable.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes all registered metric trackers.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs asynchronous cleanup of all registered metric trackers and shared sinks.
    /// Flushes all opted-in trackers before closing shared sinks once.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous cleanup operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // 1. Flush all opted-in trackers before closing shared sinks
        foreach (var tracker in _trackers.Values)
        {
            if (tracker is MetricTrackerBase trackerBase)
            {
                await trackerBase.ExecuteAutoFlushOnDisposeAsync().ConfigureAwait(false);
            }
            else if (tracker.AutoFlushOnDispose)
            {
                try
                {
                    await tracker.FlushSinksAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Trackers should not prevent other resources from disposing
                }
            }
        }

        // 2. Collect all sinks across registered trackers to ensure all shared sinks are closed once
        List<IMetricSink> sinksToDispose;
        lock (_sinksLock)
        {
            sinksToDispose = _sharedSinks.Concat(_trackers.Values.SelectMany(t => t.GetSinks())).Distinct().ToList();
            _sharedSinks.Clear();
        }

        // 3. Dispose all registered trackers
        foreach (var tracker in _trackers.Values)
        {
            try
            {
                if (tracker is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (tracker is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Trackers should not prevent other resources from disposing
            }
        }
        _trackers.Clear();

        // 4. Close shared sinks once
        foreach (var sink in sinksToDispose)
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
    /// Releases the unmanaged resources used by the <see cref="MetricFlowRegistry"/> and optionally releases the managed resources.
    /// Flushes all opted-in trackers before closing shared sinks once.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
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

        // 1. Flush all opted-in trackers before closing shared sinks
        foreach (var tracker in _trackers.Values)
        {
            if (tracker is MetricTrackerBase trackerBase)
            {
                trackerBase.ExecuteAutoFlushOnDispose();
            }
            else if (tracker.AutoFlushOnDispose)
            {
                try
                {
                    tracker.FlushSinks();
                }
                catch
                {
                    // Trackers should not prevent other resources from disposing
                }
            }
        }

        // 2. Collect all sinks across registered trackers to ensure all shared sinks are closed once
        List<IMetricSink> sinksToDispose;
        lock (_sinksLock)
        {
            sinksToDispose = _sharedSinks.Concat(_trackers.Values.SelectMany(t => t.GetSinks())).Distinct().ToList();
            _sharedSinks.Clear();
        }

        // 3. Dispose all registered trackers
        foreach (var tracker in _trackers.Values)
        {
            if (tracker is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Trackers should not prevent other resources from disposing
                }
            }
        }
        _trackers.Clear();

        // 4. Close shared sinks once
        foreach (var sink in sinksToDispose)
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
}
