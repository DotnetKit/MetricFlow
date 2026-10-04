using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedConsoleWithDIExample;

internal class Program
{
    private static async Task Main(string[] _)
    {
        Console.WriteLine("=== MetricFlow Dependency Injection Console Example ===\n");

        // 1. Build DI container with MetricFlow services
        var services = new ServiceCollection();

        // 2. Configure MetricFlow with fluent builder and AddTagsEnricher helper
        services.AddMetricFlow("AdvancedConsoleDITopic", options =>
        {
            // Use new AddTagsEnricher helper for topic-level tags
            options.AddTagsEnricher(tags =>
            {
                tags["tenant_id"] = "TenantId1";
                tags["session_id"] = Guid.NewGuid().ToString();
                tags["environment"] = "Production";
            })
            .AddThroughputCounter()
            .AddExceptionCounter()
            .AddMemoryCounter()
            .AddDimensionCounter("region");
        })
        // Fluent registration of a secondary topic tracker
        .AddMetricTracker("AuditWorker", options =>
        {
            options.AddTagsEnricher(tags =>
            {
                tags["role"] = "security-auditor";
                tags["tier"] = "internal";
            });
        });

        // 3. Register application worker services into DI
        services.AddTransient<BatchProcessorService>();
        services.AddTransient<AuditService>();

        await using var provider = services.BuildServiceProvider();

        // 4. Run worker services resolved from DI
        var batchService = provider.GetRequiredService<BatchProcessorService>();
        await batchService.RunAsync(operationCount: 10);

        var auditService = provider.GetRequiredService<AuditService>();
        await auditService.RunAuditAsync();

        // 5. Output metrics using top-level IMetricFlow facade
        var metricFlow = provider.GetRequiredService<IMetricFlow>();

        Console.WriteLine("\n==================================================");
        Console.WriteLine("            METRICFLOW TELEMETRY REPORT           ");
        Console.WriteLine("==================================================");

        foreach (var tracker in metricFlow.Trackers)
        {
            Console.WriteLine(tracker.ToString());
            Console.WriteLine("--------------------------------------------------");
        }

        // 6. Programmatic access to specific snapshots via default tracker
        var defaultTracker = metricFlow.DefaultTracker!;
        var throughput = defaultTracker.GetThroughputSnapshot("BatchIngestion");
        if (throughput != null)
        {
            Console.WriteLine("=== Programmatic Throughput Query ===");
            Console.WriteLine($"Throughput Rate       : {throughput.ItemsPerSecond:N0} items/sec");
            Console.WriteLine($"Total Items Processed : {throughput.TotalItems:N0}");
            Console.WriteLine($"Average Batch Size    : {throughput.AverageItemsPerOperation:N1} items/op\n");
        }

        var dimension = defaultTracker.GetDimensionSnapshot("DynamicProcessor", "region");
        if (dimension != null)
        {
            Console.WriteLine("=== Programmatic Dimension Query ===");
            Console.WriteLine($"Dimension Name        : {dimension.DimensionName}");
            Console.WriteLine($"Tracked Operations    : {dimension.TrackedOperations:N0} ({dimension.TrackedPercentage * 100:F1}%)");
            foreach (var (region, count) in dimension.Breakdown)
            {
                Console.WriteLine($"  - {region}: {count}");
            }
            Console.WriteLine();
        }
    }
}

/// <summary>
/// Primary worker service injecting the default IMetricTracker.
/// </summary>
public class BatchProcessorService(IMetricTracker tracker)
{
    private static readonly string[] Regions = ["US", "EU", "APAC"];

    public async Task RunAsync(int operationCount)
    {
        Console.WriteLine($"Executing {operationCount} batch operations using primary IMetricTracker...\n");

        using var globalOp = tracker.Track("GlobalBatchRun");

        for (var i = 0; i < operationCount; i++)
        {
            await ExecuteSingleOperationAsync(i);
            await ExecuteTrackActionAsync(i);
            await ExecuteBatchIngestionAsync(i);
            await ExecuteDynamicRegionalBatchAsync(i);
        }
    }

    private async Task ExecuteSingleOperationAsync(int i)
    {
        using var scope = tracker.Track("SingleOperation", new() { ["operation_id"] = $"{i}" });
        await Task.Delay(2);
        _ = AllocateBuffer((i + 1) * 16);
    }

    private async Task ExecuteTrackActionAsync(int i)
    {
        try
        {
            await tracker.TrackActionAsync(
                async () =>
                {
                    await Task.Delay(3);
                    _ = AllocateBuffer((10 - i) * 32);

                    if (i % 4 == 0)
                    {
                        throw new TimeoutException($"Simulated transient timeout at index {i}");
                    }
                },
                tags: new() { ["operation_id"] = $"{10 - i}" });
        }
        catch
        {
            // Expected for demonstrating ExceptionCounter
        }
    }

    private async Task ExecuteBatchIngestionAsync(int i)
    {
        var batchSize = (i + 1) * 250;
        using var scope = tracker.TrackItems("BatchIngestion", batchSize, new() { ["batch_id"] = $"{i}" });

        await Task.Delay(4);
        _ = AllocateBuffer(16);
    }

    private async Task ExecuteDynamicRegionalBatchAsync(int i)
    {
        var region = Regions[i % Regions.Length];
        using var scope = tracker.Track("DynamicProcessor", new() { ["region"] = region });

        await Task.Delay(2);
        long processedItems = (i + 1) * 150;
        scope.SetItems(processedItems);
    }

    private static byte[] AllocateBuffer(int sizeInKilobytes)
    {
        var buffer = new byte[sizeInKilobytes * 1024];
        if (buffer.Length > 0)
        {
            buffer[0] = 1;
        }
        return buffer;
    }
}

/// <summary>
/// Auxiliary service demonstrating keyed dependency injection of a secondary topic tracker.
/// </summary>
public class AuditService([FromKeyedServices("AuditWorker")] IMetricTracker auditTracker)
{
    public async Task RunAuditAsync()
    {
        Console.WriteLine("Executing audit verification using keyed IMetricTracker [FromKeyedServices(\"AuditWorker\")]...\n");

        using var scope = auditTracker.Track("SecurityComplianceCheck", new()
        {
            ["audit_type"] = "automated_scheduled",
            ["compliance_rule"] = "SOC2_CC6.1"
        });

        await Task.Delay(10);
    }
}
