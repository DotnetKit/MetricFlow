using System.Diagnostics;

namespace DotnetKit.MetricFlow.Tracker
{
    /// <summary>Default implementation of a counter based on Stopwatch helper</summary>
    public class StopWatchCounter(string name, Dictionary<string, string>? metricMetadata) : CounterBase(name, metricMetadata)
    {
        private long _fallbackStartTimestamp;

        public override void Start()
        {
            Interlocked.Exchange(ref _fallbackStartTimestamp, Stopwatch.GetTimestamp());
        }

        public override long Stop()
        {
            var start = Interlocked.Read(ref _fallbackStartTimestamp);
            return start > 0 ? Stopwatch.GetElapsedTime(start).Ticks : 0;
        }
    }
}