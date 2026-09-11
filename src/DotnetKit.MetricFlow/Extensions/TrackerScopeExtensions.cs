namespace DotnetKit.MetricFlow.Extensions
{
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
    }
}
