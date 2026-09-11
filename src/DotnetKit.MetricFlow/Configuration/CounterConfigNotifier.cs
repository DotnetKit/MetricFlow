namespace DotnetKit.MetricFlow.Configuration
{
    public record CounterConfig(string CounterName, bool Enabled);

    public interface ICounterConfigObservable
    {
        IDisposable Subscribe(Action<string, bool> onConfigChanged);
    }

    public class CounterConfigNotifier : ICounterConfigObservable
    {
        private readonly List<Action<string, bool>> _subscribers = new();
        private readonly object _lock = new();

        public IDisposable Subscribe(Action<string, bool> onConfigChanged)
        {
            lock (_lock)
            {
                _subscribers.Add(onConfigChanged);
            }

            return new Unsubscriber(() =>
            {
                lock (_lock)
                {
                    _subscribers.Remove(onConfigChanged);
                }
            });
        }

        public void NotifyChanged(string counterName, bool enabled)
        {
            Action<string, bool>[] snapshot;
            lock (_lock)
            {
                snapshot = _subscribers.ToArray();
            }

            foreach (var subscriber in snapshot)
            {
                subscriber(counterName, enabled);
            }
        }

        private sealed class Unsubscriber(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;

            public void Dispose()
            {
                Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
            }
        }
    }
}
