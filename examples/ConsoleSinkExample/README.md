# Console Log Sink Example (`ConsoleSinkExample`)

This example demonstrates the structured **Console Log Sink** (`ConsoleMetricSink`) and **Timer-Free Lifecycle Triggers** (`MetricSinkTriggerOptions`) introduced in **MetricFlow**.

---

## Overview

MetricFlow provides a high-performance, structured console log sink (`ConsoleMetricSink`) that formats and writes metric snapshots directly to standard output (or any configured `TextWriter`). 

Rather than relying on polling background timers or dedicated background threads, MetricFlow introduces **execution-lifecycle sampling triggers** evaluated during operation completion (`Track(...)` disposal or `Out(...)`). This ensures **zero idle CPU overhead**, making it ideal for high-throughput microservices, serverless workloads (AWS Lambda, Azure Functions), and CLI utilities.

---

## Key Features

- **Structured Single-Line Log Output**: Metric snapshots are rendered into concise, single-line log entries with timestamps, topic/sink prefixes, and counter names.
- **ANSI Color Coding**: Built-in ANSI colors highlight normal rates (green), warnings/errors (red), and metric targets (yellow/cyan).
- **Execution-Lifecycle Sampling Triggers**:
  - **Stride Sampling (`EmitEveryNExecutions`)**: Automatically emits snapshots every $N$ executions (e.g., every 5 or 100 operations).
  - **Latency / Slow-Operation Sampling (`EmitOnSlowDurationThreshold`)**: Immediately emits snapshots when an operation exceeds a duration threshold (e.g., > 100ms).
  - **Tail / Outlier Sampling (`EmitOnFailure`)**: Immediately emits snapshots when an operation fails logically (`SetFailed(true)`) or throws an unhandled exception.
  - **Probabilistic Sampling (`EmitSampleRate`)**: Emits snapshots based on a random percentage.
- **Dependency Injection & Fluent Builder**: Seamless configuration via `services.AddMetricFlow(...).AddConsoleSink(...)` or `builder.AddConsoleSink(...)`.
- **Manual / Graceful Shutdown Flush**: Explicitly flush all pending snapshots via `tracker.FlushSinks()` or `await tracker.FlushSinksAsync()`.

---

## Code Walkthrough

### 1. Dependency Injection Configuration

Register `ConsoleMetricSink` and configure lifecycle triggers within `AddMetricFlow`:

```csharp
var services = new ServiceCollection();

services.AddMetricFlow("OrderService", options =>
{
    // Configure business tags and telemetry counters
    options.AddTagsEnricher(tags =>
    {
        tags["environment"] = "Production";
        tags["region"] = "us-east-1";
    })
    .AddThroughputCounter()
    .AddFailureCounter();

    // 1. Configure the ConsoleMetricSink
    options.AddConsoleSink(opt =>
    {
        opt.Colorize = true;                 // ANSI color formatting
        opt.IncludeTimestamp = true;         // [HH:mm:ss.fff] timestamp prefix
        opt.TimestampFormat = "HH:mm:ss.fff";
        opt.Prefix = "[OrderService:Sink]";  // Custom log line prefix
    });

    // 2. Configure timer-free sampling triggers
    options.ConfigureSinkTriggers(trig =>
    {
        trig.EmitEveryNExecutions = 5;                              // Stride: every 5 executions
        trig.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100); // Latency: > 100ms
        trig.EmitOnFailure = true;                                  // Tail: on errors / exceptions
    });
});

services.AddTransient<OrderFulfillmentService>();
await using var provider = services.BuildServiceProvider();
```

### 2. Operational Workflows & Triggers

When operations run in application services, the sink triggers fire automatically:

```csharp
// Stride Trigger: Triggers every 5 operations
for (int i = 1; i <= 10; i++)
{
    using (_tracker.TrackItems("ProcessOrder", itemCount: i * 10))
    {
        await Task.Delay(10);
    }
}

// Latency Trigger: Triggers immediately if duration exceeds 100ms
using (_tracker.Track("ProcessOrder"))
{
    await Task.Delay(125);
}

// Failure Trigger: Triggers immediately on logical failure
using (var scope = _tracker.Track("ProcessOrder"))
{
    await Task.Delay(10);
    scope.SetFailed(true);
}

// Exception Trigger: Triggers immediately on unhandled exception
try
{
    await _tracker.TrackActionAsync("ProcessOrder", async () =>
    {
        throw new InvalidOperationException("Payment gateway timeout");
    });
}
catch (InvalidOperationException) { }
```

### 3. Standalone Tracker & Manual Flush

For CLI tools or background jobs without DI:

```csharp
var options = new MetricFlowOptions { Topic = "WarehouseService" }
    .AddThroughputCounter()
    .AddFailureCounter();

options.AddConsoleSink(opt =>
{
    opt.Colorize = true;
    opt.Prefix = "[Warehouse:Sink]";
});

using var tracker = new MetricTracker(options);

using (tracker.TrackItems("InventoryRestock", itemCount: 50))
{
    await Task.Delay(15);
}

// Flush all registered sinks at shutdown
tracker.FlushSinks();
```

---

## Running the Example

Run the project directly from the repository root:

```bash
dotnet run --project examples/ConsoleSinkExample/ConsoleSinkExample.csproj
```

### Sample Output

```text
================================================================
        MetricFlow - Structured Console Log Sink Example        
================================================================

>>> [Scenario 1] Dependency Injection & Timer-Free Lifecycle Triggers <<<

Step 1: Processing 10 regular orders (triggers every 5 executions via EmitEveryNExecutions)...
[20:07:55.277] [OrderService:Sink] [Duration:ProcessOrder] [Duration] Metric: ProcessOrder | Duration (min, max, avg): 11.02 ms / 13.61 ms / 11.55 ms | Total duration: 57.75 ms
[20:07:55.278] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 150, Operations: 5, Rate: 2597.2 items/s
[20:07:55.278] [OrderService:Sink] [Failure:ProcessOrder] Operations: 5, Failed: 0 (0.0%)
[20:07:55.341] [OrderService:Sink] [Duration:ProcessOrder] [Duration] Metric: ProcessOrder | Duration (min, max, avg): 10.21 ms / 13.61 ms / 11.13 ms | Total duration: 111.33 ms
[20:07:55.341] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 550, Operations: 10, Rate: 4940.1 items/s
[20:07:55.341] [OrderService:Sink] [Failure:ProcessOrder] Operations: 10, Failed: 0 (0.0%)

Step 2: Processing slow order (triggers immediately via EmitOnSlowDurationThreshold > 100ms)...
[20:07:55.467] [OrderService:Sink] [Duration:ProcessOrder] [Duration] Metric: ProcessOrder | Duration (min, max, avg): 10.21 ms / 125.95 ms / 21.57 ms | Total duration: 237.28 ms
[20:07:55.467] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 551, Operations: 11, Rate: 2322.2 items/s
[20:07:55.467] [OrderService:Sink] [Failure:ProcessOrder] Operations: 11, Failed: 0 (0.0%)

Step 3: Processing order with logical failure (triggers immediately via EmitOnFailure)...
[20:07:55.478] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 552, Operations: 12, Rate: 2221.7 items/s, Failed: 1
[20:07:55.478] [OrderService:Sink] [Failure:ProcessOrder] Operations: 12, Failed: 1 (8.3%)

Step 4: Processing order that throws an exception (triggers immediately via EmitOnFailure)...
[20:07:55.491] [OrderService:Sink] [Exception:ProcessOrder] [Exception] Metric: ProcessOrder | Exceptions / Operations: 1 / 13 (7.69%) | Exceptions Breakdown: |   - InvalidOperationException: 1
[20:07:55.491] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 553, Operations: 13, Rate: 2124.3 items/s, Failed: 2
[20:07:55.491] [OrderService:Sink] [Failure:ProcessOrder] Operations: 13, Failed: 2 (15.4%)

>>> [Scenario 2] Standalone MetricTracker & Manual Sink Flush <<<

Tracking operations in standalone tracker (no automatic triggers configured)...
Notice no sink lines were emitted yet because no triggers were set.
Now manually flushing sinks at application shutdown via FlushSinks()...

[20:07:55.540] [Warehouse:Sink] [Duration:InventoryRestock] [Duration] Metric: InventoryRestock | Duration (min, max, avg): 16.05 ms / 16.09 ms / 16.07 ms | Total duration: 48.20 ms
[20:07:55.541] [Warehouse:Sink] [Throughput:InventoryRestock] TotalItems: 150, Operations: 3, Rate: 3112.3 items/s
[20:07:55.541] [Warehouse:Sink] [Failure:InventoryRestock] Operations: 3, Failed: 0 (0.0%)

Structured console log sink example completed successfully!
```
