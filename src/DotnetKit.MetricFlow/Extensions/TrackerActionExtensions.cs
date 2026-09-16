using System.Runtime.CompilerServices;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for tracking synchronous and asynchronous delegates with automatic exception and duration tracking.
/// </summary>
public static class TrackerActionExtensions
{
    /// <summary>
    /// Tracks the execution of a synchronous action. If an exception is thrown, it is recorded and rethrown.
    /// </summary>
    public static void TrackAction(
        this IMetricTracker tracker,
        string metricName,
        Action action,
        Dictionary<string, string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(action);

        using var scope = tracker.Track(metricName, tags);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Tracks the execution of a synchronous action with the metric name resolved dynamically via [CallerMemberName].
    /// </summary>
    public static void TrackAction(
        this IMetricTracker tracker,
        Action action,
        Dictionary<string, string>? tags = null,
        [CallerMemberName] string metricName = "")
    {
        TrackAction(tracker, metricName, action, tags);
    }

    /// <summary>
    /// Tracks the execution of a synchronous function returning a value. If an exception is thrown, it is recorded and rethrown.
    /// </summary>
    public static T TrackAction<T>(
        this IMetricTracker tracker,
        string metricName,
        Func<T> func,
        Dictionary<string, string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(func);

        using var scope = tracker.Track(metricName, tags);
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Tracks the execution of a synchronous function returning a value with the metric name resolved dynamically via [CallerMemberName].
    /// </summary>
    public static T TrackAction<T>(
        this IMetricTracker tracker,
        Func<T> func,
        Dictionary<string, string>? tags = null,
        [CallerMemberName] string metricName = "")
    {
        return TrackAction(tracker, metricName, func, tags);
    }

    /// <summary>
    /// Tracks the execution of an asynchronous task. If an exception is thrown, it is recorded and rethrown.
    /// </summary>
    public static async Task TrackActionAsync(
        this IMetricTracker tracker,
        string metricName,
        Func<Task> action,
        Dictionary<string, string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(action);

        using var scope = tracker.Track(metricName, tags);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Tracks the execution of an asynchronous task with the metric name resolved dynamically via [CallerMemberName].
    /// </summary>
    public static Task TrackActionAsync(
        this IMetricTracker tracker,
        Func<Task> action,
        Dictionary<string, string>? tags = null,
        [CallerMemberName] string metricName = "")
    {
        return TrackActionAsync(tracker, metricName, action, tags);
    }

    /// <summary>
    /// Tracks the execution of an asynchronous task returning a value. If an exception is thrown, it is recorded and rethrown.
    /// </summary>
    public static async Task<T> TrackActionAsync<T>(
        this IMetricTracker tracker,
        string metricName,
        Func<Task<T>> func,
        Dictionary<string, string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(func);

        using var scope = tracker.Track(metricName, tags);
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Tracks the execution of an asynchronous task returning a value with the metric name resolved dynamically via [CallerMemberName].
    /// </summary>
    public static Task<T> TrackActionAsync<T>(
        this IMetricTracker tracker,
        Func<Task<T>> func,
        Dictionary<string, string>? tags = null,
        [CallerMemberName] string metricName = "")
    {
        return TrackActionAsync(tracker, metricName, func, tags);
    }
}
