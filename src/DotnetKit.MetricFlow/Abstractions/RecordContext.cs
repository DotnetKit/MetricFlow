namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Represents a point-in-time functional metric recording event without an operation start/end lifecycle.
/// </summary>
public readonly struct RecordContext(
    string metricName,
    long value = 1,
    IReadOnlyDictionary<string, string>? tags = null,
    IReadOnlyDictionary<string, long>? metadata = null,
    DateTime? utcTimestamp = null)
{
    /// <summary>
    /// Gets the name of the metric being recorded.
    /// </summary>
    public string MetricName { get; } = metricName;

    /// <summary>
    /// Gets the incremental value recorded for this event. Defaults to 1.
    /// </summary>
    public long Value { get; } = value;

    /// <summary>
    /// Gets optional key-value string tags (business dimensions) associated with this event.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; } = tags;

    /// <summary>
    /// Gets optional numeric metadata associated with this event.
    /// </summary>
    public IReadOnlyDictionary<string, long>? Metadata { get; } = metadata;

    /// <summary>
    /// Gets the UTC timestamp when this event was recorded.
    /// </summary>
    public DateTime UtcTimestamp { get; } = utcTimestamp ?? DateTime.UtcNow;
}
