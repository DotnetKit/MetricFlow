namespace SimpleMetricCountersExample
{
    public interface IPipelineObserver<in T>
    {
        void OnCompleted();
        Task OnErrorAsync(T value, Exception exception);
        Task OnExitAsync(T value);
        Task OnEnterAsync(T value);

    }
}
