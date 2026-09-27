namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Top-level facade providing access to metric trackers and metric aggregation across topics.
/// </summary>
public interface IMetricFlow
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
    /// Gets a metric tracker by topic name using indexer syntax.
    /// </summary>
    /// <param name="topic">The metric topic name.</param>
    /// <returns>The metric tracker instance.</returns>
    IMetricTracker this[string topic] => GetTracker(topic);
}
