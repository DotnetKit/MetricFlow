using System.Diagnostics;
using DotnetKit.MetricFlow.Tracker.Abstractions;

namespace DotnetKit.MetricFlow.Tracker
{
    public class CodeTracker<T> : IDisposable
        where T : ICounter
    {
        private readonly IMetricTracker<T> _counters;
        private readonly string _metricName;
        private readonly Dictionary<string, string>? _metricMetadata;
        private readonly long _startTimestamp;

        private bool _disposed = false;

        public CodeTracker(IMetricTracker<T> counters, string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            _counters = counters;
            _metricName = metricName;
            _metricMetadata = metricMetadata;
            _startTimestamp = Stopwatch.GetTimestamp();
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
                var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
                _counters.Out(_metricName, _metricMetadata, failed: false, duration: elapsed);
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