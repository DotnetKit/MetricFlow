# Advanced Console Example

This example demonstrates how to build an end-to-end, multi-counter telemetry pipeline with **MetricFlow** in a standalone .NET application. It highlights duration tracking, throughput analysis (fixed and dynamic batch sizing), memory allocation measurement, exception counting, dimensional slicing, caller member name inference, and programmatic metric snapshot querying.

---

## Highlights

- **Multi-Counter Pipeline**: Chains built-in counters fluent-style:
  - `DurationCounter` (default): Measures operation latency, min, max, avg, and execution counts.
  - `ThroughputCounter` (`AddThroughputCounter`): Computes processing velocity (`items/sec`), total items, and average batch sizes.
  - `ExceptionCounter` (`AddExceptionCounter`): Tracks failure rates and categorizes exceptions by type.
  - `MemoryCounter` (`AddMemoryCounter`): Tracks managed memory allocations per operation without invasive profiling.
  - `DimensionCounter` (`AddDimensionCounter`): Aggregates operation counts across business dimensions (e.g. `region`) with cardinality protection.
- **Dynamic Metric Name Inference (`[CallerMemberName]`)**: Infers the metric name directly from the calling method when omitted.
- **Batch Volume Tracking**:
  - **Upfront Sizing**: `tracker.TrackItems("BatchIngestion", batchSize)`
  - **Dynamic Sizing**: `tracker.Track("DynamicProcessor")` followed by `scope.SetItems(count)` (or `scope.SetItemCount(count)`).
- **Manual vs Scoped Tracking**: Combines scoped `using` disposable blocks with explicit manual tracking (`tracker.In` / `tracker.Out`).
- **Programmatic Snapshot Access**: Directly queries typed metric snapshots via `tracker.GetThroughputValues(...)` and `tracker.GetDimensionValues(...)`.

---

## Code Architecture

### 1. Initializing the Tracker with Multi-Counter Pipeline

```csharp
var tracker = new MetricTracker("AdvancedConsoleTopic", new()
    {
        ["tenant_id"] = "TenantId1",
        ["session_id"] = Guid.NewGuid().ToString()
    })
    .AddThroughputCounter()
    .AddExceptionCounter()
    .AddMemoryCounter()
    .AddDimensionCounter("region");
```

### 2. Operational Workflows

```csharp
// 1. CallerMemberName inference & Memory allocation
internal static async Task ExecuteOperation1Async(MetricTracker tracker, int i)
{
    // Metric name resolves automatically to "ExecuteOperation1Async"
    using var op1 = tracker.Track(tags: new() { ["operation_id"] = $"{i}" });
    await Task.Delay(2);
    _ = AllocateMemory((i + 1) * 16, (byte)i); // Exercises MemoryCounter
}

// 2. Delegate tracking & Exception capture
internal static async Task ExecuteOperation2Async(MetricTracker tracker, int i)
{
    try
    {
        await tracker.TrackActionAsync(async () =>
        {
            await Task.Delay(4);
            _ = AllocateMemory((OperationCount - i) * 32, (byte)i);
            ThrowModuloException(i); // Exercises ExceptionCounter
        },
        tags: new() { ["operation_id"] = $"{OperationCount - i}" });
    }
    catch
    {
        // Handled to let the loop continue
    }
}

// 3. Upfront batch throughput tracking
internal static async Task ExecuteBatchIngestionAsync(MetricTracker tracker, int i)
{
    var batchSize = (i + 1) * 250;
    using var scope = tracker.TrackItems("BatchIngestion", batchSize, new() { ["batch_id"] = $"{i}" });
    await Task.Delay(5);
}

// 4. Dynamic batch throughput & dimensional tagging
internal static async Task ExecuteDynamicBatchAsync(MetricTracker tracker, int i)
{
    var regions = new[] { "US", "EU", "APAC" };
    using var scope = tracker.Track("DynamicProcessor", new() { ["region"] = regions[i % regions.Length] });
    await Task.Delay(3);

    long processedCount = (i + 1) * 100;
    scope.SetItems(processedCount); // Or scope.SetItemCount(processedCount)
}
```

### 3. Programmatic Telemetry Querying

In addition to `tracker.ToString()`, you can query strongly-typed values directly:

```csharp
// Query Throughput metrics
var throughput = tracker.GetThroughputValues("BatchIngestion");
if (throughput != null)
{
    Console.WriteLine($"Rate       : {throughput.ItemsPerSecond:N0} items/sec");
    Console.WriteLine($"Total Items: {throughput.TotalItems:N0}");
    Console.WriteLine($"Avg Batch  : {throughput.AverageItemsPerOperation:N1} items/op");
}

// Query Dimension metrics
var dimension = tracker.GetDimensionValues("DynamicProcessor", "region");
if (dimension != null)
{
    Console.WriteLine($"Tracked Operations: {dimension.TrackedOperations:N0} ({dimension.TrackedPercentage * 100:F1}%)");
    foreach (var (region, count) in dimension.Breakdown)
    {
        Console.WriteLine($"  - {region}: {count}");
    }
}
```

---

## Running the Example

From the repository root:

```bash
dotnet run --project examples/AdvancedConsoleExample/AdvancedConsoleExample.csproj
```

### Sample Output

```text
Executing advanced operations with MetricFlow...

AdvancedConsoleTopic
Topic Tags:
  - tenant_id: TenantId1
  - session_id: ...

GlobalOperation
Duration (ms):
  Avg: 184.21  Min: 184.21  Max: 184.21  Total: 184.21
  Operations: 1 in / 1 out (0 failed)

BatchIngestion
Duration (ms):
  Avg: 6.84  Min: 5.92  Max: 9.15  Total: 68.42
  Operations: 10 in / 10 out (0 failed)
Throughput:
  Total Items: 13,750  Rate: 200,964 items/sec  Avg Batch: 1,375.0 items/op

DynamicProcessor
Duration (ms):
  Avg: 4.12  Min: 3.51  Max: 5.67  Total: 41.20
  Operations: 10 in / 10 out (0 failed)
Throughput:
  Total Items: 5,500  Rate: 133,495 items/sec  Avg Batch: 550.0 items/op
Dimension Breakdown [region]:
  Tracked: 10 / 10 (100.0%)
  - US: 4 (40.0%)
  - EU: 3 (30.0%)
  - APAC: 3 (30.0%)

=== Throughput Summary ===
BatchIngestion Rate  : 200,964 items/sec
Total Items Processed: 13,750
Average Batch Size   : 1,375.0 items/op

=== Dimension Summary ===
Dimension : region
Tracked Operations: 10 (100.0%)
  - US: 4
  - EU: 3
  - APAC: 3
```
