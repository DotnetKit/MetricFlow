using DotnetKit.MetricFlow.Tracker.Abstractions;
using DotnetKit.MetricFlow.Tracker.Counters;

namespace CustomCounters
{
    /// <summary>
    /// Counter based on UTC time using Pattern A state token.
    /// </summary>
    public class UtcCounter(string name = "UtcDuration") : CounterBase(name)
    {
        private readonly DurationCounter _inner = new(name);

        public override object? OnIn(in InContext context)
        {
            if (!IsEnabled) return null;
            return DateTimeOffset.UtcNow.Ticks;
        }

        public override void OnOut(object? state, in OutContext context)
        {
            if (!IsEnabled) return;
            TimeSpan elapsed = TimeSpan.Zero;
            if (state is long startTicks && startTicks > 0)
            {
                elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - startTicks);
            }
            _inner.OnOut(state, new OutContext(context.MetricName, context.Failed, context.Exception, elapsed, context.Tags));
        }

        public override IMetricSnapshot? GetSnapshot(string metricName) => _inner.GetSnapshot(metricName);
        public override IEnumerable<IMetricSnapshot> GetAllSnapshots() => _inner.GetAllSnapshots();
        public override void Reset() => _inner.Reset();
    }
}