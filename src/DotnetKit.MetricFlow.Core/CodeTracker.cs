using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{
    public class CodeTracker<T> : IDisposable
        where T : ICounter
    {
        private readonly IMetricCounters<T> _counters;
        private readonly string _blockname;
        private bool _disposed = false;

        public CodeTracker(IMetricCounters<T> counters, string blockname)

        {
            _counters = counters;
            _blockname = blockname;
            _counters.In(_blockname);
        }

        public string Name => _blockname;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // invoke disposer
                _counters.Out(_blockname);
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}