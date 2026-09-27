using System.Collections.Concurrent;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Central registry managing all metric trackers across topics.
/// </summary>
public class MetricFlowRegistry : IMetricFlow, IDisposable
{
    private readonly ConcurrentDictionary<string, IMetricTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, IMetricTracker>? _trackerFactory;
    private readonly string _defaultTopic;
    private volatile IMetricTracker? _defaultTracker;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricFlowRegistry"/>.
    /// </summary>
    /// <param name="defaultTopic">The fallback topic name when no tracker is registered.</param>
    /// <param name="initialTrackers">Optional initial trackers to register.</param>
    /// <param name="trackerFactory">Optional factory to produce trackers dynamically for unconfigured topics.</param>
    public MetricFlowRegistry(
        string defaultTopic = "Application",
        IEnumerable<IMetricTracker>? initialTrackers = null,
        Func<string, IMetricTracker>? trackerFactory = null)
    {
        _defaultTopic = string.IsNullOrWhiteSpace(defaultTopic) ? "Application" : defaultTopic;
        _trackerFactory = trackerFactory;

        if (initialTrackers != null)
        {
            foreach (var tracker in initialTrackers)
            {
                RegisterTracker(tracker);
            }
        }
    }

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

        return _trackers.GetOrAdd(topic, key =>
        {
            if (_trackerFactory != null)
            {
                var factoryTracker = _trackerFactory(key);
                if (_defaultTracker == null)
                {
                    _defaultTracker = factoryTracker;
                }
                return factoryTracker;
            }

            var fallback = new MetricTracker(key);
            if (_defaultTracker == null)
            {
                _defaultTracker = fallback;
            }
            return fallback;
        });
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
    }

    /// <summary>
    /// Disposes all registered metric trackers that implement IDisposable.
    /// </summary>
    public void Dispose()
    {
        foreach (var tracker in _trackers.Values)
        {
            if (tracker is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _trackers.Clear();
    }
}
