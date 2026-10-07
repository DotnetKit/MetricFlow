using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Defines a destination for emitting metric snapshots (e.g. console logging, structured logging, file, or database).
/// </summary>
public interface IMetricSink
{
    /// <summary>
    /// Gets the friendly name of this metric sink.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Synchronously emits a collection of metric snapshots to the sink destination.
    /// Default implementation delegates to <see cref="EmitAsync(IReadOnlyList{IMetricSnapshot}, CancellationToken)"/>.
    /// </summary>
    /// <param name="snapshots">The metric snapshots to emit.</param>
    void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
    {
        EmitAsync(snapshots).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously emits a collection of metric snapshots to the sink destination.
    /// Default implementation delegates to synchronous <see cref="Emit(IReadOnlyList{IMetricSnapshot})"/>.
    /// </summary>
    /// <param name="snapshots">The metric snapshots to emit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask EmitAsync(IReadOnlyList<IMetricSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        Emit(snapshots);
        return ValueTask.CompletedTask;
    }
}
