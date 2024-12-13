namespace DotnetKit.MetricFlow.Core.Abstractions
{
    using System.Collections.Concurrent;

    public interface IMetricCounters<T>
        where T : ICounter
    {
        string Topic { get; }

        ConcurrentDictionary<string, T> Get();

        long In(string counterName);

        IDisposable Track(string counterName);

        long Out(string counterName);

        void Clear();
    }
}