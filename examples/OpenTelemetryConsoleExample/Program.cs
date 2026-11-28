using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace OpenTelemetryConsoleExample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("===================================================================");
        Console.WriteLine("    MetricFlow + OpenTelemetry .NET Integration Example");
        Console.WriteLine("===================================================================\n");

        // 1. Configure MetricFlow and OpenTelemetry via dedicated DI extension
        var services = new ServiceCollection();
        services.AddOrderProcessingMetrics();

        using var serviceProvider = services.BuildServiceProvider();

        // 2. Resolve the tracker and MeterProvider from the DI container
        var tracker = serviceProvider.GetRequiredService<IMetricTracker>();
        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

        Console.WriteLine("1. Executing operations with batch items and regional tags...\n");

        // Simulate operations with item batching and tags
        string[] regions = ["EU", "US", "APAC"];
        for (var i = 0; i < 6; i++)
        {
            var region = regions[i % regions.Length];
            using var scope = tracker.Track("ProcessOrderBatch", new() { ["region"] = region });
            scope.SetItems(25 * (i + 1));
            await Task.Delay(15);
        }

        Console.WriteLine(
            "2. Executing operations with high-cardinality tags (testing cardinality guard)...\n"
        );

        // Simulate 8 distinct customer IDs with MaxUniqueTagValues = 3
        for (var i = 1; i <= 8; i++)
        {
            using (tracker.Track("CustomerLookup", new() { ["customer_id"] = $"cust_{i:000}" }))
            {
                await Task.Delay(5);
            }
        }

        Console.WriteLine("3. Executing an operation that throws an exception...\n");

        try
        {
            tracker.TrackAction(
                "ValidatePayment",
                () => throw new InvalidOperationException("Payment gateway timeout simulation.")
            );
        }
        catch (InvalidOperationException)
        {
            // Exception already recorded once on ValidatePayment with status="error"
        }

        Console.WriteLine(
            "4. Recording pure functional business metrics (Record / Increment)...\n"
        );

        string[] plans = ["Free", "Pro", "Enterprise"];
        for (var i = 0; i < 90; i++)
        {
            var region = regions[i % regions.Length];
            var plan = plans[i % plans.Length];
            tracker.Record("OrderPlaced", new() { ["region"] = region, ["plan"] = plan });
            tracker.Increment(
                "ItemsSold",
                (i + 1) * 3,
                new() { ["region"] = region, ["plan"] = plan }
            );
        }

        Console.WriteLine(
            "\n5. Flushing OpenTelemetry MeterProvider to terminal (raw BCL measurements):\n"
        );
        var flushed = meterProvider.ForceFlush(10_000);
        Console.WriteLine($"[OTLP] ForceFlush returned: {flushed}");

        Console.WriteLine("\n6. MetricFlow In-Memory Aggregated Snapshot Report:\n");
        Console.WriteLine(tracker.ToString());

        Console.WriteLine(
            "Done! Both OpenTelemetry export and MetricFlow local reporting succeeded."
        );
    }
}
