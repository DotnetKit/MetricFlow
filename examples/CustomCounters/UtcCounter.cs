using DotnetKit.MetricFlow.Tracker.Abstractions;

namespace CustomCounters
{
    /// <summary>
    ///  Counter based on UTC time, used to measure time in ticks
    /// </summary>
    public class UtcCounter(string name, Dictionary<string, string>? metricMetadata = null) : CounterBase(name, metricMetadata)
    {
        private long _inTicks = 0;

        public override void Start()
        {
            Interlocked.Exchange(ref _inTicks, DateTimeOffset.UtcNow.Ticks);
        }

        public override long Stop()
        {
            var start = Interlocked.Read(ref _inTicks);
            return start > 0 ? DateTimeOffset.UtcNow.Ticks - start : 0;
        }
    }
}