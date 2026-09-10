namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public abstract class CounterBase(string name) : ICounter
    {
        public string Name => name;
        public virtual bool IsEnabled { get; set; } = true;

        public abstract object? OnIn(in InContext context);
        public abstract void OnOut(object? state, in OutContext context);
        public abstract IMetricSnapshot? GetSnapshot(string metricName);
        public abstract IEnumerable<IMetricSnapshot> GetAllSnapshots();
        public abstract void Reset();
    }
}