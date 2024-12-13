namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public interface ICounter
    {
        long InCount { get; }
        long OutCount { get; }
        TimeSpan TotalDuration { get; }
        TimeSpan AverageDuration { get; }
        TimeSpan MinDuration { get; }
        TimeSpan MaxDuration { get; }

        long Inc();

        long Dec();
    }
}