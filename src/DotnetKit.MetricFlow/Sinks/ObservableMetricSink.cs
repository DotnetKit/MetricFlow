using System.Collections.Concurrent;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// A metric sink that exposes snapshots via standard reactive <see cref="IObservable{T}"/> subscriptions.
/// Ideal for piping metric streams into Rx.NET queries, event buses, or custom log writers without timers.
/// </summary>
public class ObservableMetricSink : IMetricSink, IObservable<IReadOnlyList<IMetricSnapshot>>, IDisposable
{
    private readonly ConcurrentDictionary<Guid, IObserver<IReadOnlyList<IMetricSnapshot>>> _observers = new();
    private bool _disposed;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ObservableMetricSink"/>.
    /// </summary>
    /// <param name="name">Optional friendly name for this sink.</param>
    public ObservableMetricSink(string name = "Observable")
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Observable" : name;
    }

    /// <inheritdoc />
    public void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
    {
        if (_disposed || snapshots.Count == 0)
        {
            return;
        }

        foreach (var observer in _observers.Values)
        {
            try
            {
                observer.OnNext(snapshots);
            }
            catch
            {
                // Observers must not fail the caller
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<IReadOnlyList<IMetricSnapshot>> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (_disposed)
        {
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }

        var id = Guid.NewGuid();
        _observers[id] = observer;

        return new Subscription(this, id);
    }

    /// <summary>
    /// Convenience helper to subscribe via an action delegate.
    /// </summary>
    /// <param name="onNext">Delegate called whenever a batch of snapshots is emitted.</param>
    /// <returns>An <see cref="IDisposable"/> that unregisters the subscriber.</returns>
    public IDisposable Subscribe(Action<IReadOnlyList<IMetricSnapshot>> onNext)
    {
        ArgumentNullException.ThrowIfNull(onNext);
        return Subscribe(new DelegateObserver(onNext));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="ObservableMetricSink"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            foreach (var observer in _observers.Values)
            {
                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                    // Ignored during dispose
                }
            }

            _observers.Clear();
        }
    }

    private void Unsubscribe(Guid id)
    {
        _observers.TryRemove(id, out _);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ObservableMetricSink _sink;
        private readonly Guid _id;
        private bool _disposed;

        public Subscription(ObservableMetricSink sink, Guid id)
        {
            _sink = sink;
            _id = id;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _sink.Unsubscribe(_id);
            }
        }
    }

    private sealed class DelegateObserver : IObserver<IReadOnlyList<IMetricSnapshot>>
    {
        private readonly Action<IReadOnlyList<IMetricSnapshot>> _onNext;

        public DelegateObserver(Action<IReadOnlyList<IMetricSnapshot>> onNext)
        {
            _onNext = onNext;
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(IReadOnlyList<IMetricSnapshot> value) => _onNext(value);
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}
