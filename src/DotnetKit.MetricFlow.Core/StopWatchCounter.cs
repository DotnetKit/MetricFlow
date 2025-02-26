using System.Diagnostics;

namespace DotnetKit.MetricFlow.Core
{
    /// <summary>Default implementation of a counter based on Stopwatch helper</summary>
    public class StopWatchCounter(string name, Dictionary<string, string>? metricMetadata) : CounterBase(name, metricMetadata)
    {
        private readonly Stopwatch _watcher = new Stopwatch();

        public override void Start()
        {
            _watcher.Restart();
        }
        public override long Stop()
        {
            _watcher.Stop();
            return _watcher.ElapsedTicks;
        }

    }
}