namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Configuration options for <see cref="HybridTimelineStore"/>.
/// </summary>
public sealed class HybridTimelineStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entries to retain in the high-speed in-memory buffer.
    /// Default is 10,000.
    /// </summary>
    public int MemoryCapacity { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long entries stay cached in memory before being evicted to file-only queries.
    /// Default is 1 hour.
    /// </summary>
    public TimeSpan MemoryRetention { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether to enable file-based persistence alongside the in-memory cache.
    /// Default is true.
    /// </summary>
    public bool EnableFilePersistence { get; set; } = true;

    /// <summary>
    /// Options for the underlying file store when file persistence is enabled.
    /// </summary>
    public FileTimelineStoreOptions FileOptions { get; set; } = new();
}
