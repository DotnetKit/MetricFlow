namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Strongly-typed abstract base class for metric counters with custom state token.
/// </summary>
/// <typeparam name="TState">The type of the state token produced during OnIn and consumed in OnOut.</typeparam>
public abstract class CounterBase<TState>(string name) : ICounter<TState>
{
    public string Name => name;
    public virtual bool IsEnabled { get; set; } = true;

    public abstract TState OnIn(in InContext context);
    public abstract void OnOut(TState state, in OutContext context);
    public abstract IMetricSnapshot? GetSnapshot(string metricName);
    public abstract IEnumerable<IMetricSnapshot> GetAllSnapshots();
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
public abstract class CounterBase(string name) : CounterBase<object?>(name)
{
}