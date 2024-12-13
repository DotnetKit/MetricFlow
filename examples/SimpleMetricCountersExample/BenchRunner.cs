using DotnetKit.MetricFlow.Core;

namespace SimpleMetricCountersExample
{
    public static class BenchRunner
    {
        private const int OPERATION_COUNT = 10;

        public static async Task RunExample()
        {
            var perfCounters = new MetricCounters("ExecutionTime.Metrics");

            for (var i = 0; i < OPERATION_COUNT; i++)
            {
                using (var _ = perfCounters.Track("Operation1"))
                {
                    await Task.Delay(2);
                }
                using (var _ = perfCounters.Track("Operation2"))
                {
                    await Task.Delay(1);
                }
            }

            perfCounters.WriteToConsole();

            /*
                ======================================

                ExecutionTime.Metrics

                Operation1
                Count (in, out): 10 / 10
                Avg duration: 11,4463 ms
                Duration (min, max) : 2,3258 ms / 15,7493 ms
                Total duration: 98,6163 ms

                Operation2
                Count (in, out): 10 / 10
                Avg duration: 11,0366 ms
                Duration (min, max) : 1,3448 ms / 22,4824 ms
                Total duration: 125,3018 ms

                ======================================
             */

        }
    }
}