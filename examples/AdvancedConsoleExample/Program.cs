using DotnetKit.MetricFlow;

namespace AdvancedConsoleExample;

internal class Program
{
    private const int OperationCount = 10;

    private static async Task Main(string[] args)
    {
        // Initialize tracker with full multi-counter telemetry pipeline:
        // DurationCounter (default) + ThroughputCounter + ExceptionCounter + MemoryCounter + TagBreakdownCounter
        var tracker = new MetricTracker("AdvancedConsoleTopic", new()
            {
                ["tenant_id"] = "TenantId1",
                ["session_id"] = Guid.NewGuid().ToString()
            })
            .AddThroughputCounter()
            .AddExceptionCounter()
            .AddMemoryCounter()
            .AddDimensionCounter("region");

        Console.WriteLine("Executing advanced operations with MetricFlow...\n");

        tracker.In("GlobalOperation");

        for (var i = 0; i < OperationCount; i++)
        {
            await ExecuteOperation1Async(tracker, i);
            await ExecuteOperation2Async(tracker, i);
            await ExecuteBatchIngestionAsync(tracker, i);
            await ExecuteDynamicBatchAsync(tracker, i);
        }

        tracker.Out("GlobalOperation");

        // 1. Output comprehensive telemetry breakdown for all counters
        Console.WriteLine(tracker.ToString());

        // 2. Direct programmatic access to throughput metrics
        var throughput = tracker.GetThroughputValues("BatchIngestion");
        if (throughput != null)
        {
            Console.WriteLine("=== Throughput Summary ===");
            Console.WriteLine($"BatchIngestion Rate : {throughput.ItemsPerSecond:N0} items/sec");
            Console.WriteLine($"Total Items Processed: {throughput.TotalItems:N0}");
            Console.WriteLine($"Average Batch Size   : {throughput.AverageItemsPerOperation:N1} items/op\n");
        }

        // 3. Direct programmatic access to dimension breakdown metrics
        var dimension = tracker.GetDimensionValues("DynamicProcessor", "region");
        if (dimension != null)
        {
            Console.WriteLine("=== Dimension Summary ===");
            Console.WriteLine($"Dimension : {dimension.DimensionName}");
            Console.WriteLine($"Tracked Operations: {dimension.TrackedOperations:N0} ({dimension.TrackedPercentage * 100:F1}%)");
            foreach (var (region, count) in dimension.Breakdown)
            {
                Console.WriteLine($"  - {region}: {count}");
            }
        }
    }

    internal static async Task ExecuteOperation1Async(MetricTracker tracker, int i)
    {
        // Operation1: tracks duration and memory allocation (metric name resolved dynamically via [CallerMemberName])
        using var op1 = tracker.Track(tags: new() { ["operation_id"] = $"{i}" });

        await Task.Delay(2);

        // Allocate memory to exercise MemoryCounter (16 KB - 160 KB)
        _ = AllocateMemory((i + 1) * 16, (byte)i);
    }

    internal static async Task ExecuteOperation2Async(MetricTracker tracker, int i)
    {
        // Operation2: uses TrackActionAsync for automatic duration, memory, and exception capture
        try
        {
            await tracker.TrackActionAsync(
                async () =>
                {
                    await Task.Delay(4);

                    // Allocate memory to exercise MemoryCounter (32 KB - 320 KB)
                    _ = AllocateMemory((OperationCount - i) * 32, (byte)i);

                    // Throw exception based on modulo to exercise ExceptionCounter
                    ThrowModuloException(i);
                },
                tags: new() { ["operation_id"] = $"{OperationCount - i}" });
        }
        catch
        {
            // Handled to allow benchmark loop to continue
        }
    }

    internal static async Task ExecuteBatchIngestionAsync(MetricTracker tracker, int i)
    {
        // Batch operation: tracks batch throughput upfront via TrackItems
        var batchSize = (i + 1) * 250; // 250 to 2,500 items per batch
        using var scope = tracker.TrackItems("BatchIngestion", batchSize, new() { ["batch_id"] = $"{i}" });

        await Task.Delay(5);

        // Allocate buffer proportional to batch
        _ = AllocateMemory(16, (byte)i);
    }

    internal static async Task ExecuteDynamicBatchAsync(MetricTracker tracker, int i)
    {
        // Dynamic batch operation: item count determined during execution and tagged with regional dimension
        var regions = new[] { "US", "EU", "APAC" };
        using var scope = tracker.Track("DynamicProcessor", new() { ["region"] = regions[i % regions.Length] });

        await Task.Delay(3);

        // Dynamically compute processed message count
        long processedCount = (i + 1) * 100;
        scope.SetItems(processedCount);
    }

    internal static byte[] AllocateMemory(int sizeInKilobytes, byte touchByte = 1)
    {
        var buffer = new byte[sizeInKilobytes * 1024];
        if (buffer.Length > 0)
        {
            buffer[0] = touchByte;
        }

        return buffer;
    }

    internal static void ThrowModuloException(int i)
    {
        if (i % 4 == 0)
        {
            throw (i % 2 == 0)
                ? new TimeoutException($"Operation2 timeout at iteration {i}")
                : new TaskCanceledException($"Operation2 canceled at iteration {i}");
        }
    }
}
