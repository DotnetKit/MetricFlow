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
            Count (in, out): 1 / 1
            Avg duration: 1336323 ms
            Duration (min, max) : 1336323 ms / 1336323 ms
            Total duration: 1336323 ms
            2

            Operation1
            MetricMetadata:  operation_id:0
            Count (in, out): 10 / 10
            Avg duration: 29906 ms
            Duration (min, max) : 22745 ms / 182873 ms
            Total duration: 615900 ms
            2

            Operation2
            MetricMetadata:  operation_id:10
            Count (in, out): 10 / 10
            Avg duration: 112361 ms
            Duration (min, max) : 41157 ms / 166233 ms
            Total duration: 695333 ms
            ======================================
             */

        }
    }
}