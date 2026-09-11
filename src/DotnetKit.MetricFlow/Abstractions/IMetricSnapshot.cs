namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public interface IMetricSnapshot
    {
        string MetricName { get; }
        string CounterName { get; }
        DateTime Timestamp { get; }
        string ToFormattedString();
    }
}
