namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Top-level facade providing access to metric trackers and metric aggregation across topics.
/// </summary>
public interface IMetricFlow : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the metric tracker associated with the specified topic.
    /// If no tracker exists, one is created and registered.
    /// </summary>
    /// <param name="topic">The metric topic name.</param>
    /// <returns>The metric tracker instance.</returns>
    IMetricTracker GetTracker(string topic);

    /// <summary>
    /// Tries to get the metric tracker associated with the specified topic.
    /// </summary>
    /// <param name="topic">The metric topic name.</param>
    /// <param name="tracker">When this method returns, contains the tracker if found; otherwise, null.</param>
    /// <returns>True if the tracker was found; otherwise, false.</returns>
    bool TryGetTracker(string topic, out IMetricTracker? tracker);

    /// <summary>
    /// Gets the default metric tracker, if any is configured.
    /// </summary>
    IMetricTracker? DefaultTracker { get; }

    /// <summary>
    /// Gets all registered metric trackers.
    /// </summary>
    IEnumerable<IMetricTracker> Trackers { get; }

    /// <summary>
    /// Gets all shared metric sinks owned by this registry or facade.
    /// </summary>
    IEnumerable<Sinks.IMetricSink> SharedSinks => [];

    /// <summary>
    /// Gets a metric tracker by topic name using indexer syntax.
    /// </summary>
    /// <param name="topic">The metric topic name.</param>
    /// <returns>The metric tracker instance.</returns>
    IMetricTracker this[string topic] => GetTracker(topic);

    /// <summary>
    /// Synchronously flushes all active snapshots across all registered topic trackers to their respective sinks.
    /// </summary>
    void FlushAllSinks()
    {
        foreach (var tracker in Trackers)
        {
            tracker.FlushSinks();
        }
    }

    /// <summary>
    /// Asynchronously flushes all active snapshots across all registered topic trackers to their respective sinks.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous flush operation.</returns>
    async ValueTask FlushAllSinksAsync(CancellationToken cancellationToken = default)
    {
        foreach (var tracker in Trackers)
        {
            await tracker.FlushSinksAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    void IDisposable.Dispose() => GC.SuppressFinalize(this);

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
