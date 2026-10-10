namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Defines the contract for an individual metric counter that records operation in/out lifecycle events and produces snapshots.
/// </summary>
public interface ICounter : IMetricSnapshotsSource
{
    /// <summary>
    /// Gets the unique name identifying this counter type (e.g. "Duration", "Throughput", "Exception").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this counter is actively collecting metrics.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Invoked when an operation begins. Returns an optional state object to correlate with the operation's completion.
    /// </summary>
    /// <param name="context">The operation entry context including metric name, tags, and metadata.</param>
    /// <returns>An optional state object to pass into <see cref="OnOut"/>, or <c>null</c>.</returns>
    object? OnIn(in InContext context);

    /// <summary>
    /// Invoked when an operation completes.
    /// </summary>
    /// <param name="state">The state object produced by <see cref="OnIn"/>.</param>
    /// <param name="context">The operation exit context including duration, failure status, and exception.</param>
    void OnOut(object? state, in OutContext context);

    /// <summary>
    /// Gets the latest metric snapshot for the specified metric name.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    /// <returns>The snapshot instance, or <c>null</c> if no measurements exist for the given metric.</returns>
    IMetricSnapshot? GetSnapshot(string metricName);

    /// <summary>
    /// Resets all accumulated metric data for this counter.
    /// </summary>
    void Reset();
}

/// <summary>
/// Defines a strongly-typed contract for metric counters that pass typed state between <see cref="OnIn"/> and <see cref="OnOut"/>.
/// </summary>
/// <typeparam name="TState">The type of state correlated between entry and exit.</typeparam>
public interface ICounter<TState> : ICounter
{
    /// <summary>
    /// Invoked when an operation begins. Returns strongly-typed state to correlate with operation exit.
    /// </summary>
    /// <param name="context">The operation entry context.</param>
    /// <returns>The typed state instance.</returns>
    new TState OnIn(in InContext context);

    /// <summary>
    /// Invoked when an operation completes with strongly-typed state.
    /// </summary>
    /// <param name="state">The typed state produced by <see cref="OnIn"/>.</param>
    /// <param name="context">The operation exit context.</param>
    void OnOut(TState state, in OutContext context);

    object? ICounter.OnIn(in InContext context) => OnIn(in context);

    void ICounter.OnOut(object? state, in OutContext context)
    {
        if (state is TState typedState)
        {
            OnOut(typedState, in context);
        }
        else if (state is null)
        {
            OnOut(default!, in context);
        }
    }
}