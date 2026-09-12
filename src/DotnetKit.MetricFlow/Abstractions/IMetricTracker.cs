using System.Runtime.CompilerServices;

namespace DotnetKit.MetricFlow.Abstractions;

public interface IMetricTracker : IMetricSnapshotsSource
{
    string Topic { get; }
    Dictionary<string, string>? TopicTags { get; }

    IMetricTracker RegisterCounter(ICounter counter);
    bool UnregisterCounter(string counterName);
    void SetCounterEnabled(string counterName, bool enabled);
    IEnumerable<ICounter> GetCounters();

    IDisposable Track(string metricName, Dictionary<string, string>? tags = null);
    IDisposable Track(Dictionary<string, string>? tags = null, [CallerMemberName] string metricName = "");

    void In(string metricName, Dictionary<string, string>? tags = null);
    void In(Dictionary<string, string>? tags = null, [CallerMemberName] string metricName = "");

    void Out(
        string metricName,
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null);
    void Out(
        Dictionary<string, string>? tags = null,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        [CallerMemberName] string metricName = "");

    IMetricSnapshot? GetSnapshot(string metricName, string counterName);
    IEnumerable<IMetricSnapshot> GetSnapshots(string metricName);

    void Clear();
}