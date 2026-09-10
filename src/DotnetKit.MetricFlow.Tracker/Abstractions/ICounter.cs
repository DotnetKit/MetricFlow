namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public interface ICounter
    {
        string Name { get; }
        bool IsEnabled { get; set; }
        object? OnIn(in InContext context);
        void OnOut(object? state, in OutContext context);
        IMetricSnapshot? GetSnapshot(string metricName);
        IEnumerable<IMetricSnapshot> GetAllSnapshots();
        void Reset();
    }
}