using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Core.Extensions;

namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public abstract class MetricTrackerBase<T>(string topic,
     Func<string, Dictionary<string, string>?, T> counterFactory,
     Dictionary<string, string>? topicTags = null,
     int? samplingRate = 100) : IMetricTracker<T>
        where T : ICounter
    {
        private readonly ConcurrentDictionary<string, T> _blockCounters = new ConcurrentDictionary<string, T>();
        private readonly Random _randomizer = new Random();
        public string Topic => topic;

        public Dictionary<string, string>? TopicTags => topicTags;

        public long? In(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            if (IsSampled(samplingRate, _randomizer))
            {
                return 0;
            }
            var counter = _blockCounters.GetOrAdd(metricName, counterFactory(metricName, metricMetadata));
            // Handle metadata as needed
            return counter.Inc();
        }

        public long? Out(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            if (IsSampled(samplingRate, _randomizer))
            {
                return 0;
            }
            var counter = _blockCounters.GetOrAdd(metricName, counterFactory(metricName, metricMetadata));
            // Handle metadata as needed
            return counter.Dec();
        }

        public IDisposable Track(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            return new CodeTracker<T>(this, metricName, metricMetadata);
        }

        public IEnumerable<T> GetCounters() => _blockCounters.Values;

        public void Clear() => _blockCounters.Clear();

        public CounterValues? GetValues(string metricName)
        {
            if (_blockCounters.TryGetValue(metricName, out var counter))
            {
                return counter.Values;
            }
            return null;
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Topic);
            if (TopicTags != null)
            {
                sb.AppendLine(TopicTags.ToFormattedString("Topic Tags"));
            }
            foreach (var counter in GetCounters())
            {
                sb.AppendLine(counter.ToString());
            }
            return sb.ToString();
        }
        private static bool IsSampled(int? samplingRate, Random randomizer)
        {
            return !samplingRate.HasValue || (randomizer.NextDouble() * 100) <= 100 - samplingRate.Value;
        }

    }
}