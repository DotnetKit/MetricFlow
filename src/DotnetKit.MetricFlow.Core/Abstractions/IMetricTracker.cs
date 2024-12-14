namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public interface IMetricTracker<T>
       where T : ICounter
    {
        Dictionary<string, string>? TopicMetadata { get; }
        string Topic { get; }

        IEnumerable<T> GetCounters();

        long In(string counterName, Dictionary<string, string>? topicMetadata = null);

        IDisposable Track(string counterName, Dictionary<string, string>? topicMetadata = null);

        long Out(string counterName, Dictionary<string, string>? topicMetadata = null);

        void Clear();
    }
}