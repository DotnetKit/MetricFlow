using DotnetKit.MetricFlow.Core;

namespace SimpleMetricCountersExample
{
    public static class BenchRunner
    {
        private const int OPERATION_COUNT = 10;

        public static async Task RunExample()
        {
            var tracker = new MetricTracker("ExecutionTimeMetricsTopic", new() { ["tenant_id"] = "TenantId1" });

            for (var i = 0; i < OPERATION_COUNT; i++)
            {
                using (var _ = tracker.Track("Operation1", new() { ["metric_operation1_id"] = i.ToString() }))
                {
                    await Task.Delay(2);
                }
                using (var _ = tracker.Track("Operation2", new() { ["metric_operation2_id"] = i.ToString() }))
                {

                    await Task.Delay(4);
                }
            }

            Console.WriteLine(tracker.ToString());

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