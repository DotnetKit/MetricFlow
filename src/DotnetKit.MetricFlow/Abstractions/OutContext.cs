namespace DotnetKit.MetricFlow.Abstractions;

public readonly ref struct OutContext
{
    public string MetricName { get; }
    public bool Failed { get; }
    public Exception? Exception { get; }
    public TimeSpan? Duration { get; }
    public IReadOnlyDictionary<string, string>? Tags { get; }
    public IReadOnlyDictionary<string, long>? Metadata { get; }
    public DateTime UtcTimestamp { get; }

    public OutContext(
        string metricName,
        bool failed = false,
        Exception? exception = null,
        TimeSpan? duration = null,
        IReadOnlyDictionary<string, string>? tags = null,
        IReadOnlyDictionary<string, long>? metadata = null,
        DateTime? utcTimestamp = null)
    {
        MetricName = metricName;
        Failed = failed || exception != null;
        Exception = exception;
        Duration = duration;
        Tags = tags;
        Metadata = metadata;
        UtcTimestamp = utcTimestamp ?? DateTime.UtcNow;
    }
}
