using DotnetKit.MetricFlow;

namespace BasicConsoleExample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // 1. Initialize tracker with topic name and optional topic tags
        // By default, MetricTracker registers DurationCounter with zero additional counters.
        var tracker = new MetricTracker("BasicConsoleTopic", new()
        {
            ["environment"] = "Development"
        });

        Console.WriteLine("Executing operations with MetricTracker...\n");

        // 2. Scoped tracking with using statement
        for (var i = 1; i <= 5; i++)
        {
            using (tracker.Track("ProcessOrder", new() { ["order_id"] = $"{i}" }))
            {
                await Task.Delay(10);
            }
        }

        // 3. Delegate tracking with TrackAction
        for (var i = 1; i <= 3; i++)
        {
            tracker.TrackAction("ValidatePayment", () => Thread.Sleep(5));
        }

        // 4. Print formatted telemetry snapshots
        Console.WriteLine(tracker.ToString());
    }
}
