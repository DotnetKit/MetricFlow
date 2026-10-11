using System.Text;

namespace DotnetKit.MetricFlow.Hierarchy;

/// <summary>
/// Represents a single execution node in a hierarchical parent-child metric tracking tree.
/// </summary>
public class HierarchyNode
{
    private readonly List<HierarchyNode> _children = [];
    private readonly object _syncRoot = new();

    /// <summary>
    /// Gets the operation or metric name for this node.
    /// </summary>
    public string MetricName { get; }

    /// <summary>
    /// Gets the UTC timestamp when this operation started.
    /// </summary>
    public DateTime StartTimeUtc { get; }

    /// <summary>
    /// Gets the total wall-clock duration of this operation, including all child operations.
    /// </summary>
    public TimeSpan Duration { get; internal set; }

    /// <summary>
    /// Gets the exclusive duration spent inside this operation, subtracting child operations' durations.
    /// </summary>
    public TimeSpan SelfDuration { get; internal set; }

    /// <summary>
    /// Gets the total managed memory allocated on the current thread during this operation in bytes.
    /// </summary>
    public long AllocatedBytes { get; internal set; }

    /// <summary>
    /// Gets the exclusive managed memory allocated by this operation in bytes, subtracting child operations' allocations.
    /// </summary>
    public long SelfAllocatedBytes { get; internal set; }

    /// <summary>
    /// Gets the count of items processed (for throughput tracking), if recorded via SetItems or metadata.
    /// </summary>
    public long ItemCount { get; internal set; }

    /// <summary>
    /// Gets whether this operation completed with a failure or exception.
    /// </summary>
    public bool Failed { get; internal set; }

    /// <summary>
    /// Gets the exception thrown during this operation, if any.
    /// </summary>
    public Exception? Exception { get; internal set; }

    /// <summary>
    /// Gets optional business tags attached to this operation.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Tags { get; internal set; }

    /// <summary>
    /// Gets optional technical metadata attached to this operation.
    /// </summary>
    public IReadOnlyDictionary<string, long>? Metadata { get; internal set; }

    /// <summary>
    /// Gets the parent node in the hierarchy, or <c>null</c> if this is the root node.
    /// </summary>
    public HierarchyNode? Parent { get; }

    /// <summary>
    /// Gets the child nodes executed within the scope of this operation.
    /// </summary>
    public IReadOnlyList<HierarchyNode> Children
    {
        get
        {
            lock (_syncRoot)
            {
                return _children.ToArray();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="HierarchyNode"/>.
    /// </summary>
    /// <param name="metricName">The operation or metric name.</param>
    /// <param name="parent">The parent node, or null for root.</param>
    /// <param name="startTimeUtc">Optional start timestamp. Defaults to <see cref="DateTime.UtcNow"/>.</param>
    public HierarchyNode(string metricName, HierarchyNode? parent = null, DateTime? startTimeUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        MetricName = metricName;
        Parent = parent;
        StartTimeUtc = startTimeUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a child execution node to this node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    internal void AddChild(HierarchyNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        lock (_syncRoot)
        {
            _children.Add(child);
        }
    }
}
