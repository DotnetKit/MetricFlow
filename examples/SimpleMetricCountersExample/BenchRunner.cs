using DotnetKit.MetricFlow.Tracker;    

namespace SimpleMetricCountersExample
{
    public static class BenchRunner
    {
        private const int OPERATION_COUNT = 10;

        public static async Task RunExample()
        {
            var tracker = new MetricTracker("ExecutionTimeMetricsTopic", new()
            {
                ["tenant_id"] = "TenantId1",
                ["session_id"] = Guid.NewGuid().ToString()
            });

            tracker.In("GlobalOperation");

            for (var i = 0; i < OPERATION_COUNT; i++)
            {

                using (var __ = tracker.Track("Operation1", new() { ["operation_id"] = $"{i}" }))
                {
                    await Task.Delay(2);
                }
                using (var __ = tracker.Track("Operation2", new() { ["operation_id"] = $"{OPERATION_COUNT - i}" }))
                {

                    await Task.Delay(4);
                }
            }

            tracker.Out("GlobalOperation");

            Console.WriteLine(tracker.ToString());

            /*
            ======================================
            ExecutionTimeMetricsTopic
            Topic Tags:  tenant_id:TenantId1, session_id:74f627d9-5787-42b1-bab6-f1953ac3e215
            GlobalOperation
            MetricMetadata:
            Count (in, out, failed): 1 / 1 / 0
            Avg duration: 70.1 ms
            Duration (min, max) : 70.1 ms / 70.1 ms
            Total duration: 70.1 ms

            Operation1
            MetricMetadata:  operation_id:0
            Count (in, out, failed): 10 / 10 / 0
            Avg duration: 2.4 ms
            Duration (min, max) : 2.2 ms / 3.3 ms
            Total duration: 24.0 ms

            Operation2
            MetricMetadata:  operation_id:10
            Count (in, out, failed): 10 / 10 / 0
            Avg duration: 4.4 ms
            Duration (min, max) : 3.1 ms / 4.6 ms
            Total duration: 44.0 ms
            ======================================
             */

        }
    }
}