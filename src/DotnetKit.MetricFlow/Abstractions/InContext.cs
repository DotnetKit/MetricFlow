namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Represents the execution context provided to counters when an operation begins.
/// </summary>
public readonly ref struct InContext
{
    /// <summary>
    /// Gets the name of the metric or operation being initiated.
    /// </summary>
    public string MetricName { get; }

    /// <summary>
    /// Gets the key-value tags attached to this operation, if any.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// Gets numeric metadata attached to this operation, if any.
    /// </summary>
    public IReadOnlyDictionary<string, long>? Metadata { get; }

    /// <summary>
    /// Gets the UTC timestamp when the operation entered.
    /// </summary>
    public DateTime UtcTimestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InContext"/> ref struct.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="utcTimestamp">Optional explicit entry timestamp; defaults to <see cref="DateTime.UtcNow"/>.</param>
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
