using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.OpenTelemetry;
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

        // 1. Configure MetricFlow and OpenTelemetry together in DI using .WithOpenTelemetry()
        var services = new ServiceCollection();

        services.AddMetricFlow("OrderProcessingService", options =>
        {
            options.AddTagsEnricher(tags =>
            {
                tags["environment"] = "Production";
                tags["datacenter"] = "eu-central-1";
            });

            // Add built-in throughput counter for local snapshots
            options.AddThroughputCounter();

            // Configure meter cardinality protection to protect Prometheus / OTLP collectors
            options.ConfigureMeters(m =>
            {
                m.MaxUniqueTagValues = 3;
                m.OverflowBucket = "[Other]";
            });
        })
        .WithOpenTelemetry(otel =>
        {
            otel.WithMetrics(metrics =>
            {
                // Export directly to console (in production, use .AddOtlpExporter() or .AddPrometheusExporter())
                metrics.AddConsoleExporter((_, metricReaderOptions) =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                });
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        // 2. Resolve the tracker and MeterProvider from the DI container
        var tracker = serviceProvider.GetRequiredService<IMetricTracker>();
        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

        Console.WriteLine("1. Executing operations with batch items and regional tags...\n");

        // Simulate operations with item batching and tags
        string[] regions = ["EU", "US", "APAC"];
        for (int i = 0; i < 6; i++)
        {
            var region = regions[i % regions.Length];
            using (var scope = tracker.Track("ProcessOrderBatch", new() { ["region"] = region }))
            {
                scope.SetItems(25 * (i + 1));
                await Task.Delay(15);
            }
        }

        Console.WriteLine("2. Executing operations with high-cardinality tags (testing cardinality guard)...\n");

        // Simulate 8 distinct customer IDs with MaxUniqueTagValues = 3
        for (int i = 1; i <= 8; i++)
        {
            using (tracker.Track("CustomerLookup", new() { ["customer_id"] = $"cust_{i:000}" }))
            {
                await Task.Delay(5);
            }
        }

        Console.WriteLine("3. Executing an operation that throws an exception...\n");

        try
        {
            using (tracker.Track("ValidatePayment"))
            {
                throw new InvalidOperationException("Payment gateway timeout simulation.");
            }
        }
        catch (Exception ex)
        {
            tracker.Out("ValidatePayment", failed: true, exception: ex);
        }

        Console.WriteLine("\n4. Flushing OpenTelemetry MeterProvider to terminal (raw BCL measurements):\n");
        meterProvider.ForceFlush();

        Console.WriteLine("\n5. MetricFlow In-Memory Aggregated Snapshot Report:\n");
        Console.WriteLine(tracker.ToString());

        Console.WriteLine("Done! Both OpenTelemetry export and MetricFlow local reporting succeeded.");
    }
}
