﻿using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Core.Extensions;

namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public abstract class MetricTrackerBase<T>(string topic, Func<string, Dictionary<string, string>?, T> counterFactory, Dictionary<string, string>? topicMetadata = null) : IMetricTracker<T>
        where T : ICounter
    {
        private readonly ConcurrentDictionary<string, T> _blockCounters = new ConcurrentDictionary<string, T>();

        public string Topic => topic;

        public Dictionary<string, string>? TopicMetadata => topicMetadata;

        public long In(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            var counter = _blockCounters.GetOrAdd(metricName, counterFactory(metricName, metricMetadata));
            // Handle metadata as needed
            return counter.Inc();
        }

        public long Out(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
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
            if (TopicMetadata != null)
            {
                sb.AppendLine(TopicMetadata.ToFormattedString("TopicMetadata"));
            }
            foreach (var counter in GetCounters())
            {
                sb.AppendLine(counter.ToString());
            }
            return sb.ToString();
        }

    }
}