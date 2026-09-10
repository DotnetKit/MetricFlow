namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public interface IMetricTracker
    {
        string Topic { get; }
        Dictionary<string, string>? TopicTags { get; }

        IMetricTracker RegisterCounter(ICounter counter);
        bool UnregisterCounter(string counterName);
        void SetCounterEnabled(string counterName, bool enabled);
        IEnumerable<ICounter> GetCounters();

        IDisposable Track(string metricName, Dictionary<string, string>? tags = null);
        void In(string metricName, Dictionary<string, string>? tags = null);
        void Out(
            string metricName,
            Dictionary<string, string>? tags = null,
            bool failed = false,
            Exception? exception = null,
            TimeSpan? duration = null);

        IMetricSnapshot? GetSnapshot(string metricName, string counterName);
        IEnumerable<IMetricSnapshot> GetSnapshots(string metricName);
        IEnumerable<IMetricSnapshot> GetAllSnapshots();

        void Clear();
    }
}