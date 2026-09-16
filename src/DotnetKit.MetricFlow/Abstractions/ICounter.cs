namespace DotnetKit.MetricFlow.Abstractions;

public interface ICounter : IMetricSnapshotsSource
{
    string Name { get; }
    bool IsEnabled { get; set; }
    object? OnIn(in InContext context);
    void OnOut(object? state, in OutContext context);
    IMetricSnapshot? GetSnapshot(string metricName);
    void Reset();
}

public interface ICounter<TState> : ICounter
{
    new TState OnIn(in InContext context);
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