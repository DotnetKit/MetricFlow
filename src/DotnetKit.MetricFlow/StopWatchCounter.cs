using DotnetKit.MetricFlow.Tracker.Counters;

namespace DotnetKit.MetricFlow.Tracker
{
    /// <summary>
    /// StopWatchCounter provides high-resolution duration tracking based on Stopwatch.
    /// </summary>
    public class StopWatchCounter : DurationCounter
    {
        public StopWatchCounter(string name = DefaultCounterName) : base(name)
        {
        }
    }
}