using System.Collections.Concurrent;

namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public abstract class MetricCountersBase<T> : IMetricCounters<T>
        where T : ICounter, new()
    {
        private readonly string _topic;
        private readonly ConcurrentDictionary<string, T> _blockCounters = new ConcurrentDictionary<string, T>();

        public MetricCountersBase(string topic)
        {
            _topic = topic;
        }

        public string Topic => _topic;

        public long In(string metricName)
        {
            if (!_blockCounters.ContainsKey(metricName))
            {
                _blockCounters.TryAdd(metricName, new T());
            }
            return _blockCounters[metricName].Inc();
        }

        public long Out(string metricName)
        {
            if (!_blockCounters.ContainsKey(metricName))
            {
                _blockCounters.TryAdd(metricName, new T());
            }
            return _blockCounters[metricName].Dec();
        }

        public IDisposable Track(string metricName)
        {
            return new CodeTracker<T>(this, metricName);
        }

        public ConcurrentDictionary<string, T> Get()
        {
            return _blockCounters;
        }

        public void Clear()
        {
            _blockCounters.Clear();
        }
    }
}