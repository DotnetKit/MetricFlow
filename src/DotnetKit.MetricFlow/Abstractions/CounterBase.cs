namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Strongly-typed abstract base class for metric counters with custom state token.
/// </summary>
/// <typeparam name="TState">The type of the state token produced during OnIn and consumed in OnOut.</typeparam>
/// <param name="name">The unique name identifying this counter.</param>
public abstract class CounterBase<TState>(string name) : ICounter<TState>
{
    /// <inheritdoc />
    public string Name => name;

    /// <inheritdoc />
    public virtual bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public abstract TState OnIn(in InContext context);

    /// <inheritdoc />
    public abstract void OnOut(TState state, in OutContext context);

    /// <inheritdoc />
    public abstract IMetricSnapshot? GetSnapshot(string metricName);

    /// <inheritdoc />
    public abstract IEnumerable<IMetricSnapshot> GetAllSnapshots();

    /// <inheritdoc />
    public abstract void Reset();

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

/// <summary>
/// Base class for metric counters using untyped object state.
/// </summary>
/// <param name="name">The unique name identifying this counter.</param>
public abstract class CounterBase(string name) : CounterBase<object?>(name)
{
}