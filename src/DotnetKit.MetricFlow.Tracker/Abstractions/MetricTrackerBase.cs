using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotnetKit.MetricFlow.Tracker.Extensions;

namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public abstract class MetricTrackerBase<T>(string topic,
     Func<string, Dictionary<string, string>?, T> counterFactory,
     Dictionary<string, string>? topicTags = null,
     double? samplingRate = 1.0) : IMetricTracker<T>
        where T : ICounter
    {
        private readonly ConcurrentDictionary<string, T> _blockCounters = new ConcurrentDictionary<string, T>();
        private readonly AsyncLocal<Dictionary<string, Stack<long>>?> _asyncTimestamps = new();
        private readonly AsyncLocal<Dictionary<string, int>?> _asyncDroppedCounts = new();

        public string Topic => topic;

        public Dictionary<string, string>? TopicTags => topicTags;

        public long? In(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            if (ShouldDrop(samplingRate))
            {
                RecordDropped(metricName);
                return 0;
            }

            RecordStartTimestamp(metricName);
            var counter = GetOrAddCounter(metricName, metricMetadata);
            return counter.Inc();
        }

        public long? Out(string metricName, Dictionary<string, string>? metricMetadata = null, bool? failed = false, TimeSpan? duration = null)
        {
            if (TryConsumeDropped(metricName))
            {
                return 0;
            }

            if (!duration.HasValue && TryPopStartTimestamp(metricName, out var startTimestamp))
            {
                duration = Stopwatch.GetElapsedTime(startTimestamp);
            }

            var counter = GetOrAddCounter(metricName, metricMetadata);
            return duration.HasValue ? counter.Dec(duration.Value, failed) : counter.Dec(failed);
        }

        public IDisposable Track(string metricName, Dictionary<string, string>? metricMetadata = null)
        {
            if (ShouldDrop(samplingRate))
            {
                return NoOpDisposable.Instance;
            }

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

        private T GetOrAddCounter(string metricName, Dictionary<string, string>? metricMetadata)
        {
            return _blockCounters.GetOrAdd(
                metricName,
                static (name, state) => state.counterFactory(name, state.metricMetadata),
                (counterFactory, metricMetadata));
        }

        private static bool ShouldDrop(double? rate)
        {
            if (!rate.HasValue || rate.Value >= 1.0)
            {
                return false;
            }
            if (rate.Value <= 0.0)
            {
                return true;
            }
            return Random.Shared.NextDouble() > rate.Value;
        }

        private void RecordStartTimestamp(string metricName)
        {
            var dict = _asyncTimestamps.Value;
            if (dict == null)
            {
                dict = new Dictionary<string, Stack<long>>();
                _asyncTimestamps.Value = dict;
            }

            if (!dict.TryGetValue(metricName, out var stack))
            {
                stack = new Stack<long>();
                dict[metricName] = stack;
            }

            stack.Push(Stopwatch.GetTimestamp());
        }

        private bool TryPopStartTimestamp(string metricName, out long timestamp)
        {
            var dict = _asyncTimestamps.Value;
            if (dict != null && dict.TryGetValue(metricName, out var stack) && stack.Count > 0)
            {
                timestamp = stack.Pop();
                return true;
            }

            timestamp = 0;
            return false;
        }

        private void RecordDropped(string metricName)
        {
            var dict = _asyncDroppedCounts.Value;
            if (dict == null)
            {
                dict = new Dictionary<string, int>();
                _asyncDroppedCounts.Value = dict;
            }

            dict[metricName] = dict.TryGetValue(metricName, out var count) ? count + 1 : 1;
        }

        private bool TryConsumeDropped(string metricName)
        {
            var dict = _asyncDroppedCounts.Value;
            if (dict != null && dict.TryGetValue(metricName, out var count) && count > 0)
            {
                if (count == 1)
                {
                    dict.Remove(metricName);
                }
                else
                {
                    dict[metricName] = count - 1;
                }
                return true;
            }

            return false;
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}