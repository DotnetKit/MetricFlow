namespace SimpleMetricCountersExample
{
    public interface IObservable<T>
    {
        IDisposable Subscribe(IPipelineObserver<T> plugin);
        Task NotifyEnterAsync(T data);
        Task NotifyExitAsync(T data);
        Task NotifyErrorAsync(T data, Exception exception);
        void Complete();
    }
}
