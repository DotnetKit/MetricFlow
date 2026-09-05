namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public interface ICounter
    {
        string Name { get; }
        DateTime TimeStamp { get; }
        CounterValues Values { get; }
        long Inc();
        long Dec(bool? failed = false);
        long Dec(TimeSpan duration, bool? failed = false);
    }
}