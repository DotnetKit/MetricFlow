using System.Diagnostics;
using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{
    /// <summary>Default implementation of a counter based on Stopwatch helper</summary> 
    public class StopWatchCounter : CounterBase
    {
        private readonly Stopwatch _watcher;

        public StopWatchCounter()
        {
            _watcher = new Stopwatch();
        }

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