using System.Collections.Concurrent;
using System.Diagnostics;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Hierarchy;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Counter that automatically correlates parent and child operations across async execution contexts,
/// capturing full execution trees with self-time, item counts, allocated memory, and exception details.
/// </summary>
public class HierarchyCounter : ICounter
{
    /// <summary>
    /// Default counter name for the hierarchy counter.
    /// </summary>
    public const string DefaultCounterName = "Hierarchy";

    private static readonly ActivitySource ActivitySourceInstance = new(
        "DotnetKit.MetricFlow",
        typeof(HierarchyCounter).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    private readonly ConcurrentDictionary<string, HierarchyTreeSnapshot> _trees = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<HierarchyNode?> _currentScope = new();

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the current ambient <see cref="HierarchyNode"/> in the active execution context, if any.
    /// </summary>
    public HierarchyNode? CurrentNode => _currentScope.Value;

    /// <summary>
    /// Gets the shared <see cref="ActivitySource"/> used by MetricFlow for distributed tracing correlation.
    /// </summary>
    public static ActivitySource ActivitySource => ActivitySourceInstance;

    /// <summary>
    /// Initializes a new instance of <see cref="HierarchyCounter"/>.
    /// </summary>
    /// <param name="name">Optional custom counter name. Defaults to <see cref="DefaultCounterName"/>.</param>
    public HierarchyCounter(string name = DefaultCounterName)
    {
        Name = string.IsNullOrWhiteSpace(name) ? DefaultCounterName : name;
    }

    /// <inheritdoc />
    public object? OnIn(in InContext context)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var parent = _currentScope.Value;
        var node = new HierarchyNode(context.MetricName, parent, context.UtcTimestamp);
        parent?.AddChild(node);
        _currentScope.Value = node;

        long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

        Activity? activity = null;
        if (ActivitySourceInstance.HasListeners())
        {
            activity = ActivitySourceInstance.StartActivity(context.MetricName);
            if (activity != null && context.Tags != null)
            {
                foreach (var (k, v) in context.Tags)
                {
                    activity.SetTag(k, v);
                }
            }
        }

        return new HierarchyStateToken(node, startAllocatedBytes, activity);
    }

    /// <inheritdoc />
    public void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled || state is not HierarchyStateToken token)
        {
            return;
        }

        var node = token.Node;
        node.Duration = context.Duration ?? TimeSpan.Zero;

        long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        node.AllocatedBytes = endAllocatedBytes >= token.StartAllocatedBytes ? endAllocatedBytes - token.StartAllocatedBytes : 0;

        node.ItemCount = ItemCountExtractor.TryExtractItemCount(context.Tags, context.Metadata, out var items) ? items : 0;
        node.Failed = context.Failed;
        node.Exception = context.Exception;
        node.Tags = context.Tags;
        node.Metadata = context.Metadata;

        // Calculate self duration and self allocated bytes
        long childrenTicks = 0;
        long childrenAllocated = 0;
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            childrenTicks += children[i].Duration.Ticks;
            childrenAllocated += children[i].AllocatedBytes;
        }

        var childrenDuration = TimeSpan.FromTicks(childrenTicks);
        node.SelfDuration = node.Duration > childrenDuration ? node.Duration - childrenDuration : TimeSpan.Zero;
        node.SelfAllocatedBytes = Math.Max(0, node.AllocatedBytes - childrenAllocated);

        // OpenTelemetry Activity completion
        if (token.Activity != null)
        {
            if (node.Failed)
            {
                token.Activity.SetStatus(ActivityStatusCode.Error, node.Exception?.Message);
                if (node.Exception != null)
                {
                    token.Activity.SetTag("exception.type", node.Exception.GetType().FullName);
                    token.Activity.SetTag("exception.message", node.Exception.Message);
                    token.Activity.SetTag("exception.stacktrace", node.Exception.StackTrace);
                }
            }
            token.Activity.SetTag("metricflow.self_duration_ms", node.SelfDuration.TotalMilliseconds);
            token.Activity.SetTag("metricflow.allocated_bytes", node.AllocatedBytes);
            if (node.ItemCount > 0)
            {
                token.Activity.SetTag("metricflow.items", node.ItemCount);
            }
            token.Activity.Dispose();
        }

        // Restore parent scope
        _currentScope.Value = node.Parent;

        // If root node, create and register final snapshot
        if (node.Parent == null)
        {
            var snapshot = new HierarchyTreeSnapshot(node.MetricName, Name, node, DateTime.UtcNow);
            _trees[node.MetricName] = snapshot;
        }
    }

    /// <inheritdoc />
    public IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _trees.TryGetValue(metricName, out var snapshot) ? snapshot : null;
    }

    /// <inheritdoc />
    public IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        return _trees.Values;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _trees.Clear();
    }

    private sealed record HierarchyStateToken(HierarchyNode Node, long StartAllocatedBytes, Activity? Activity);
}
