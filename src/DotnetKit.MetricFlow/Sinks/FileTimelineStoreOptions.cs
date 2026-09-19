namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Configuration options for <see cref="FileTimelineStore"/>.
/// </summary>
public sealed class FileTimelineStoreOptions
{
    /// <summary>
    /// Gets or sets the root directory where timeline NDJSON files are stored.
    /// Default is "metrics-timeline".
    /// </summary>
    public string DirectoryPath { get; set; } = "metrics-timeline";

    /// <summary>
    /// Gets or sets whether to partition files by instance/pod identifier (e.g. {Directory}/{InstanceId}/timeline-yyyy-MM-dd.ndjson).
    /// Highly recommended in multi-instance environments (Kubernetes, ECS) to eliminate concurrent file lock contention.
    /// Default is true.
    /// </summary>
    public bool PartitionByInstance { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to flush file streams immediately after writing each batch.
    /// Default is true.
    /// </summary>
    public bool AutoFlush { get; set; } = true;
}
