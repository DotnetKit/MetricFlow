using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotnetKit.MetricFlow.Configuration;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow.Abstractions
{
    public abstract class MetricTrackerBase : IMetricTracker
    {
        private readonly ConcurrentDictionary<string, ICounter> _counters = new(StringComparer.OrdinalIgnoreCase);
        private volatile ICounter[] _activeCounters = [];

        private readonly AsyncLocal<Dictionary<string, Stack<InOperationState>>?> _asyncOperations = new();
        private readonly AsyncLocal<Dictionary<string, int>?> _asyncDroppedCounts = new();

        private readonly string _topic;
        private readonly Dictionary<string, string>? _topicTags;
        private readonly double? _samplingRate;

        public string Topic => _topic;
        public Dictionary<string, string>? TopicTags => _topicTags;
        public double? SamplingRate => _samplingRate;

        protected MetricTrackerBase(
            string topic,
            Dictionary<string, string>? topicTags = null,
            double? samplingRate = 1.0,
            ICounterConfigObservable? configObservable = null)
        {
            _topic = topic;
            _topicTags = topicTags;
            _samplingRate = samplingRate;

            configObservable?.Subscribe(OnCounterConfigChanged);
        }

        public IMetricTracker RegisterCounter(ICounter counter)
        {
            ArgumentNullException.ThrowIfNull(counter);
            _counters[counter.Name] = counter;
            RebuildActiveCounters();
            return this;
        }

        public bool UnregisterCounter(string counterName)
        {
            var removed = _counters.TryRemove(counterName, out _);
            if (removed)
            {
                RebuildActiveCounters();
            }
            return removed;
        }

        public void SetCounterEnabled(string counterName, bool enabled)
        {
            if (_counters.TryGetValue(counterName, out var counter))
            {
                counter.IsEnabled = enabled;
                RebuildActiveCounters();
            }
        }

        public IEnumerable<ICounter> GetCounters() => _counters.Values;

        public ICounter? GetCounter(string counterName)
        {
            _counters.TryGetValue(counterName, out var counter);
            return counter;
        }

        public IDisposable Track(string metricName, Dictionary<string, string>? tags = null)
        {
            if (ShouldDrop(_samplingRate))
            {
                return NoOpDisposable.Instance;
            }

            var active = _activeCounters;
            if (active.Length == 0)
            {
                return NoOpDisposable.Instance;
            }

            return new CodeTracker(active, metricName, tags);
        }

        public void In(string metricName, Dictionary<string, string>? tags = null)
        {
            if (ShouldDrop(_samplingRate))
            {
                RecordDropped(metricName);
                return;
            }

            var active = _activeCounters;
            var states = new object?[active.Length];
            var inContext = new InContext(metricName, tags);

            for (int i = 0; i < active.Length; i++)
            {
                states[i] = active[i].OnIn(in inContext);
            }

            RecordInOperation(metricName, new InOperationState(Stopwatch.GetTimestamp(), active, states));
        }

        public void Out(
            string metricName,
            Dictionary<string, string>? tags = null,
            bool failed = false,
            Exception? exception = null,
            TimeSpan? duration = null)
        {
            if (TryConsumeDropped(metricName))
            {
                return;
            }

            InOperationState? opState = TryPopInOperation(metricName);

            if (!duration.HasValue && opState != null)
            {
                duration = Stopwatch.GetElapsedTime(opState.StartTimestamp);
            }

            var outContext = new OutContext(metricName, failed, exception, duration, tags);

            if (opState != null)
            {
                for (int i = 0; i < opState.Counters.Length; i++)
                {
                    if (opState.Counters[i].IsEnabled)
                    {
                        opState.Counters[i].OnOut(opState.States[i], in outContext);
                    }
                }
            }
            else
            {
                // Fallback: If Out was called without a preceding In on this async context
                var active = _activeCounters;
                for (int i = 0; i < active.Length; i++)
                {
                    if (active[i].IsEnabled)
                    {
                        active[i].OnOut(null, in outContext);
                    }
                }
            }
        }

        public IMetricSnapshot? GetSnapshot(string metricName, string counterName)
        {
            return _counters.TryGetValue(counterName, out var counter) ? counter.GetSnapshot(metricName) : null;
        }

        public IEnumerable<IMetricSnapshot> GetSnapshots(string metricName)
        {
            foreach (var counter in _counters.Values)
            {
                var snapshot = counter.GetSnapshot(metricName);
                if (snapshot != null)
                {
                    yield return snapshot;
                }
            }
        }

        public IEnumerable<IMetricSnapshot> GetAllSnapshots()
        {
            foreach (var counter in _counters.Values)
            {
                foreach (var snapshot in counter.GetAllSnapshots())
                {
                    yield return snapshot;
                }
            }
        }

        public void Clear()
        {
            foreach (var counter in _counters.Values)
            {
                counter.Reset();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Topic);
            if (TopicTags != null)
            {
                var tagsDict = new Dictionary<string, string>(TopicTags);
                sb.AppendLine(tagsDict.ToFormattedString("Topic Tags"));
            }

            var snapshotsByOperation = GetAllSnapshots()
                .GroupBy(s => s.MetricName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in snapshotsByOperation)
            {
                foreach (var snapshot in group)
                {
                    sb.AppendLine(snapshot.ToFormattedString());
                }
            }

            return sb.ToString();
        }

        private void OnCounterConfigChanged(string counterName, bool enabled)
        {
            SetCounterEnabled(counterName, enabled);
        }

        private void RebuildActiveCounters()
        {
            _activeCounters = _counters.Values.Where(c => c.IsEnabled).ToArray();
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

        private void RecordInOperation(string metricName, InOperationState opState)
        {
            var dict = _asyncOperations.Value;
            if (dict == null)
            {
                dict = new Dictionary<string, Stack<InOperationState>>();
                _asyncOperations.Value = dict;
            }

            if (!dict.TryGetValue(metricName, out var stack))
            {
                stack = new Stack<InOperationState>();
                dict[metricName] = stack;
            }

            stack.Push(opState);
        }

        private InOperationState? TryPopInOperation(string metricName)
        {
            var dict = _asyncOperations.Value;
            if (dict != null && dict.TryGetValue(metricName, out var stack) && stack.Count > 0)
            {
                return stack.Pop();
            }
            return null;
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

        private sealed class InOperationState(long startTimestamp, ICounter[] counters, object?[] states)
        {
            public long StartTimestamp => startTimestamp;
            public ICounter[] Counters => counters;
            public object?[] States => states;
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}