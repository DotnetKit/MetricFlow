using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow;

/// <summary>
/// StopWatchCounter provides high-resolution duration tracking based on Stopwatch.
/// </summary>
public class StopWatchCounter : DurationCounter
{
    public StopWatchCounter(string name = DefaultCounterName) : base(name)
    {
    }
}