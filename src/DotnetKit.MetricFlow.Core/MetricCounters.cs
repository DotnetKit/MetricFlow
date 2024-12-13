using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core
{

    public class MetricCounters(string topic) : MetricCountersBase<StopWatchCounter>(topic)
    {
        public void WriteToConsole()
        {
            throw new NotImplementedException();
        }
    }
}