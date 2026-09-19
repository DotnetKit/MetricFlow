namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for tracking scopes returned by IMetricTracker.Track.
/// </summary>
public static class TrackerScopeExtensions
{
    /// <summary>
    /// Records an exception on the active tracking scope and marks the operation as failed.
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="exception">The exception to record.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetException(this IDisposable scope, Exception? exception)
    {
        if (scope is CodeTracker codeTracker)
        {
            codeTracker.SetException(exception);
        }
        return scope;
    }

    /// <summary>
    /// Marks the active tracking scope as failed or succeeded (logical failure, not exception based).
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="failed">Whether the operation failed. Defaults to true.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetFailed(this IDisposable scope, bool failed = true)
    {
        if (scope is CodeTracker codeTracker)
        {
            codeTracker.SetFailed(failed);
        }
        return scope;
    }

    /// <summary>
    /// Adds or updates a tag on the active tracking scope.
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetTag(this IDisposable scope, string key, string value)
    {
        if (scope is CodeTracker codeTracker)
        {
            codeTracker.SetTag(key, value);
        }
        return scope;
    }

    /// <summary>
    /// Adds or updates a numeric metadata entry on the active tracking scope for technical telemetry and counter consumption.
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The numeric metadata value.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetMetadata(this IDisposable scope, string key, long value)
    {
        if (scope is CodeTracker codeTracker)
        {
            codeTracker.SetMetadata(key, value);
        }
        return scope;
    }

    /// <summary>
    /// Records the number of processed items/entities on the active tracking scope for throughput tracking.
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="itemCount">The number of items processed.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetItems(this IDisposable scope, long itemCount)
    {
        if (scope is CodeTracker codeTracker)
        {
            codeTracker.SetItems(itemCount);
        }
        return scope;
    }

    /// <summary>
    /// Records the number of processed items/entities on the active tracking scope for throughput tracking.
    /// </summary>
    /// <param name="scope">The tracking scope disposable.</param>
    /// <param name="itemCount">The number of items processed.</param>
    /// <returns>The same tracking scope for chaining.</returns>
    public static IDisposable SetItemCount(this IDisposable scope, long itemCount) => SetItems(scope, itemCount);
}
