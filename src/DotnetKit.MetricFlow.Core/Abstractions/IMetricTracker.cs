namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public interface IMetricTracker<T>
       where T : ICounter
    {
        Dictionary<string, string>? TopicTags { get; }
        string Topic { get; }

        IEnumerable<T> GetCounters();

        long? In(string counterName, Dictionary<string, string>? topicTags = null);

        IDisposable Track(string counterName, Dictionary<string, string>? topicTags = null);

        long? Out(string counterName, Dictionary<string, string>? topicTags = null);

        void Clear();
    }
}