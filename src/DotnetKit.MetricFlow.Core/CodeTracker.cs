using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{
    public class CodeTracker<T> : IDisposable
        where T : ICounter
    {
        private readonly IMetricTracker<T> _counters;
        private readonly string _metricName;

        private bool _disposed = false;

        public CodeTracker(IMetricTracker<T> counters, string metricName, Dictionary<string, string>? metricMetadata = null)

        {
            _counters = counters;
            _metricName = metricName;
            _counters.In(_metricName, metricMetadata);

        }

        public string Name => _metricName;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // invoke disposer
                _counters.Out(_metricName);
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