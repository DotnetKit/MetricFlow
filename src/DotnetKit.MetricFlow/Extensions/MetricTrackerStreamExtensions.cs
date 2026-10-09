using System.Runtime.CompilerServices;
using DotnetKit.MetricFlow.Abstractions;

// ReSharper disable once CheckNamespace
namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for tracking asynchronous and synchronous data streams (<see cref="IAsyncEnumerable{T}"/> and <see cref="IEnumerable{T}"/>)
/// with automatic duration measurement, yielded items counting for throughput calculation, and exception tracking.
/// </summary>
public static class MetricTrackerStreamExtensions
{
    /// <summary>
    /// Tracks an asynchronous stream (<see cref="IAsyncEnumerable{T}"/>), automatically measuring the duration,
    /// counting the yielded items (for throughput counters and telemetry), and recording exceptions if enumeration fails.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The asynchronous data stream.</param>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="operationName">The name of the metric operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that tracks metrics when enumerated.</returns>
    public static IAsyncEnumerable<T> TrackStream<T>(
        this IAsyncEnumerable<T> source,
        IMetricTracker tracker,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return TrackStreamCore(source, tracker, operationName, tags: null, metadata: null, cancellationToken);
    }

    /// <summary>
    /// Tracks an asynchronous stream (<see cref="IAsyncEnumerable{T}"/>) with additional business tags and technical metadata.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The asynchronous data stream.</param>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="operationName">The name of the metric operation.</param>
    /// <param name="tags">Optional business tags to attach to the operation.</param>
    /// <param name="metadata">Optional technical metadata to attach to the operation.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that tracks metrics when enumerated.</returns>
    public static IAsyncEnumerable<T> TrackStream<T>(
        this IAsyncEnumerable<T> source,
        IMetricTracker tracker,
        string operationName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return TrackStreamCore(source, tracker, operationName, tags, metadata, cancellationToken);
    }

    /// <summary>
    /// Tracks an asynchronous stream (<see cref="IAsyncEnumerable{T}"/>) with the operation name automatically resolved via [CallerMemberName].
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The asynchronous data stream.</param>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="operationName">The calling member name.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that tracks metrics when enumerated.</returns>
    public static IAsyncEnumerable<T> TrackStream<T>(
        this IAsyncEnumerable<T> source,
        IMetricTracker tracker,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "")
    {
        return TrackStream(source, tracker, operationName, tags: null, metadata: null, cancellationToken);
    }

    /// <summary>
    /// Tracks a synchronous sequence (<see cref="IEnumerable{T}"/>), automatically measuring the duration,
    /// counting the yielded items (for throughput counters and telemetry), and recording exceptions if enumeration fails.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The sequence to track.</param>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="operationName">The name of the metric operation.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> that tracks metrics when enumerated.</returns>
    public static IEnumerable<T> TrackStream<T>(
        this IEnumerable<T> source,
        IMetricTracker tracker,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return TrackStreamSyncCore(source, tracker, operationName, tags: null, metadata: null);
    }

    /// <summary>
    /// Tracks a synchronous sequence (<see cref="IEnumerable{T}"/>) with additional business tags and technical metadata.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The sequence to track.</param>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="operationName">The name of the metric operation.</param>
    /// <param name="tags">Optional business tags to attach to the operation.</param>
    /// <param name="metadata">Optional technical metadata to attach to the operation.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> that tracks metrics when enumerated.</returns>
    public static IEnumerable<T> TrackStream<T>(
        this IEnumerable<T> source,
        IMetricTracker tracker,
        string operationName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return TrackStreamSyncCore(source, tracker, operationName, tags, metadata);
    }

    private static async IAsyncEnumerable<T> TrackStreamCore<T>(
        IAsyncEnumerable<T> source,
        IMetricTracker tracker,
        string operationName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = tracker.Track(operationName, tags, metadata);
        long itemCount = 0;

        try
        {
            var enumerator = source.WithCancellation(cancellationToken).ConfigureAwait(false).GetAsyncEnumerator();
            await using (enumerator)
            {
                while (true)
                {
                    T item;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }
                        item = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        scope.SetException(ex);
                        throw;
                    }

                    itemCount++;
                    yield return item;
                }
            }
        }
        finally
        {
            scope.SetItems(itemCount);
            scope.Dispose();
        }
    }

    private static IEnumerable<T> TrackStreamSyncCore<T>(
        IEnumerable<T> source,
        IMetricTracker tracker,
        string operationName,
        Dictionary<string, string>? tags,
        Dictionary<string, long>? metadata)
    {
        var scope = tracker.Track(operationName, tags, metadata);
        long itemCount = 0;

        try
        {
            using var enumerator = source.GetEnumerator();
            while (true)
            {
                T item;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }
                    item = enumerator.Current;
                }
                catch (Exception ex)
                {
                    scope.SetException(ex);
                    throw;
                }

                itemCount++;
                yield return item;
            }
        }
        finally
        {
            scope.SetItems(itemCount);
            scope.Dispose();
        }
    }
}
