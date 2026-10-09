using System.Collections.Concurrent;

namespace DotnetKit.MetricFlow.Meters;

/// <summary>
/// Registry managing topic-keyed IMetricMeterBridge instances and their associated Meter lifecycles.
/// </summary>
public class MetricFlowMeterRegistry(
    MetricFlowMeterOptions? defaultOptions = null,
    Func<string, MetricFlowMeterOptions>? optionsFactory = null)
    : IDisposable
{
    private readonly ConcurrentDictionary<string, IMetricMeterBridge> _bridges = new(StringComparer.OrdinalIgnoreCase);
    private readonly MetricFlowMeterOptions _defaultOptions = defaultOptions ?? new MetricFlowMeterOptions();
    private int _disposed;

    /// <summary>
    /// Gets or creates a meter bridge for the specified topic and topic-level tags.
    /// </summary>
    public IMetricMeterBridge GetOrCreateBridge(string topic, Dictionary<string, string>? topicTags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        return _bridges.GetOrAdd(topic, key =>
        {
            var options = optionsFactory != null ? optionsFactory(key) : _defaultOptions;
            return new MetricFlowMeterBridge(key, topicTags, options);
        });
    }

    /// <summary>
    /// Attempts to retrieve an existing meter bridge by topic.
    /// </summary>
    public bool TryGetBridge(string topic, out IMetricMeterBridge? bridge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return _bridges.TryGetValue(topic, out bridge);
    }

    /// <summary>
    /// Disposes all managed meter bridges and their underlying Meter instances.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MetricFlowMeterRegistry"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (disposing)
        {
            foreach (var bridge in _bridges.Values)
            {
                try
                {
                    bridge.Dispose();
                }
                catch
                {
                    // Bridges should not prevent other resources from disposing
                }
            }
            _bridges.Clear();
        }
    }
}
