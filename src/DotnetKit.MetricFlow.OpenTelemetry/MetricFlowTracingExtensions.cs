using System.Diagnostics;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.OpenTelemetry;

/// <summary>
/// Tracing and activity context correlation extensions for MetricFlow.
/// </summary>
public static class MetricFlowTracingExtensions
{
    public const string DefaultActivitySourceName = "DotnetKit.MetricFlow";

    private static readonly ActivitySource _activitySource = new(
        DefaultActivitySourceName,
        typeof(MetricFlowTracingExtensions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>
    /// Gets the MetricFlow <see cref="ActivitySource"/> instance.
    /// </summary>
    public static ActivitySource ActivitySource => _activitySource;

    /// <summary>
    /// Enriches a tag dictionary with the current ambient <see cref="Activity.Current"/> trace ID and span ID.
    /// </summary>
    public static Dictionary<string, string> WithTraceContext(this Dictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var activity = Activity.Current;
        if (activity != null)
        {
            tags["trace_id"] = activity.TraceId.ToString();
            tags["span_id"] = activity.SpanId.ToString();
        }

        return tags;
    }

    /// <summary>
    /// Starts a new tracing span correlated with the MetricFlow operation name and returns an <see cref="Activity"/> (or null if not sampled).
    /// </summary>
    public static Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return _activitySource.StartActivity(operationName, kind);
    }
}
