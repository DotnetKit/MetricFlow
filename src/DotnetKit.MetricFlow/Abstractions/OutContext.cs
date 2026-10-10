namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Represents the execution context provided to counters when an operation completes.
/// </summary>
public readonly ref struct OutContext
{
    /// <summary>
    /// Gets the name of the metric or operation that finished.
    /// </summary>
    public string MetricName { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed, either logically or due to an exception.
    /// </summary>
    public bool Failed { get; }

    /// <summary>
    /// Gets the exception thrown during the operation, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the elapsed duration of the operation, if measured.
    /// </summary>
    public TimeSpan? Duration { get; }

    /// <summary>
    /// Gets the key-value tags attached to this operation, if any.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>
    /// Gets numeric metadata attached to this operation, if any.
    /// </summary>
    public IReadOnlyDictionary<string, long>? Metadata { get; }

    /// <summary>
    /// Gets the UTC timestamp when the operation exited.
    /// </summary>
    public DateTime UtcTimestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OutContext"/> ref struct.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    /// <param name="failed">Whether the operation failed logically.</param>
    /// <param name="exception">Optional exception thrown during execution.</param>
    /// <param name="duration">Optional elapsed duration.</param>
    /// <param name="tags">Optional key-value tags.</param>
    /// <param name="metadata">Optional numeric metadata.</param>
    /// <param name="utcTimestamp">Optional explicit exit timestamp; defaults to <see cref="DateTime.UtcNow"/>.</param>
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
