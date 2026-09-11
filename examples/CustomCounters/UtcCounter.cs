using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace CustomCounters
{
    /// <summary>
    /// Counter based on UTC time using Pattern A state token.
    /// </summary>
    public class UtcCounter(string name = "UtcDuration") : CounterBase<long>(name)
    {
        private readonly DurationCounter _inner = new(name);

        public override long OnIn(in InContext context)
        {
            if (!IsEnabled) return 0;
            return DateTimeOffset.UtcNow.Ticks;
        }

        public override void OnOut(long state, in OutContext context)
        {
            if (!IsEnabled) return;
            TimeSpan elapsed = TimeSpan.Zero;
            if (state > 0)
            {
                elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - state);
            }
            _inner.OnOut(state, new OutContext(context.MetricName, context.Failed, context.Exception, elapsed, context.Tags));
        }

        public override IMetricSnapshot? GetSnapshot(string metricName) => _inner.GetSnapshot(metricName);
        public override IEnumerable<IMetricSnapshot> GetAllSnapshots() => _inner.GetAllSnapshots();
        public override void Reset() => _inner.Reset();
    }
}