namespace DotnetKit.MetricFlow.Abstractions;

public readonly ref struct InContext
{
    public string MetricName { get; }
    public IReadOnlyDictionary<string, string>? Tags { get; }
    public IReadOnlyDictionary<string, long>? Metadata { get; }
    public DateTime UtcTimestamp { get; }

    public InContext(
        string metricName,
        IReadOnlyDictionary<string, string>? tags = null,
        IReadOnlyDictionary<string, long>? metadata = null,
        DateTime? utcTimestamp = null)
    {
        MetricName = metricName;
        Tags = tags;
        Metadata = metadata;
        UtcTimestamp = utcTimestamp ?? DateTime.UtcNow;
    }
}
