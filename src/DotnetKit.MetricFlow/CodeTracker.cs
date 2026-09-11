using System.Diagnostics;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow
{
    public class CodeTracker : IDisposable
    {
        private readonly ICounter[] _counters;
        private readonly object?[] _states;
        private readonly string _metricName;
        private readonly Dictionary<string, string>? _tags;
        private readonly long _startTimestamp;

        private bool _failed;
        private Exception? _exception;
        private int _disposed;

        public string MetricName => _metricName;

        public CodeTracker(
            ICounter[] counters,
            string metricName,
            Dictionary<string, string>? tags = null)
        {
            _counters = counters;
            _metricName = metricName;
            _tags = tags;
            _startTimestamp = Stopwatch.GetTimestamp();
            _states = new object?[counters.Length];

            var inContext = new InContext(metricName, tags);
            for (int i = 0; i < counters.Length; i++)
            {
                if (counters[i].IsEnabled)
                {
                    _states[i] = counters[i].OnIn(in inContext);
                }
            }
        }

        public CodeTracker SetFailed(bool failed = true)
        {
            _failed = failed;
            return this;
        }

        public CodeTracker SetException(Exception? exception)
        {
            _exception = exception;
            if (exception != null)
            {
                _failed = true;
            }
            return this;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (disposing)
            {
                var duration = Stopwatch.GetElapsedTime(_startTimestamp);
                var outContext = new OutContext(_metricName, _failed, _exception, duration, _tags);

                for (int i = 0; i < _counters.Length; i++)
                {
                    if (_counters[i].IsEnabled)
                    {
                        _counters[i].OnOut(_states[i], in outContext);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}