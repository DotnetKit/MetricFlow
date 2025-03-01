namespace SimpleMetricCountersExample
{
    public class PipelineObservable<T> : IObservable<T>
    {
        private List<IPipelineObserver<T>> plugins;

        public PipelineObservable()
        {
            plugins = new List<IPipelineObserver<T>>();
        }


        public IDisposable Subscribe(IPipelineObserver<T> plugin)
        {
            if (!plugins.Contains(plugin))
                plugins.Add(plugin);
            return new Unsubscriber(plugins, plugin);
        }

        private class Unsubscriber : IDisposable
        {
            private List<IPipelineObserver<T>> _observers;
            private IPipelineObserver<T> _observer;

            public Unsubscriber(List<IPipelineObserver<T>> observers, IPipelineObserver<T> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }

        public async Task NotifyEnterAsync(T data)
        {
            foreach (var plugin in plugins)
            {
                await plugin.OnEnterAsync(data);
            }

        }
        public async Task NotifyErrorAsync(T data, Exception exception)
        {
            foreach (var plugin in plugins)
            {
                await plugin.OnErrorAsync(data, exception);
            }

        }
        public async Task NotifyExitAsync(T data)
        {
            foreach (var plugin in plugins)
            {
                await plugin.OnExitAsync(data);
            }
        }
        public void Complete()
        {
            foreach (var observer in plugins.ToArray())
                if (plugins.Contains(observer))
                    observer.OnCompleted();

            plugins.Clear();
        }
    }
}
