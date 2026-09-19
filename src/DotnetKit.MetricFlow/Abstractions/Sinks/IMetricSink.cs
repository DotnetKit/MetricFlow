namespace DotnetKit.MetricFlow.Abstractions.Sinks;

/// <summary>
/// Defines a sink that receives batches of metric timeline entries.
/// </summary>
public interface IMetricSink
{
    /// <summary>
    /// Gets the unique or descriptive name of this sink.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Emits a batch of harvested timeline entries to the sink.
    /// </summary>
    /// <param name="entries">The list of harvested timeline entries.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask EmitAsync(IReadOnlyList<MetricTimelineEntry> entries, CancellationToken cancellationToken = default);
}
