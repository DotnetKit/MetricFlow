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
- **Native Threshold Color Coding & Dynamic Selectors**: Declarative `opt.AddThreshold<TSnapshot>(warn, critical, ...)` and dynamic `opt.ColorSelector = snapshot => ...` pattern matching.
- **Configurable Units & Custom Formatters**: Customizable duration units (`DurationUnit.Auto`, `Seconds`, etc.), memory units (`MemoryUnit.Auto`, `Megabytes`, etc.), throughput labels (`ThroughputUnit`), and strongly-typed snapshot formatters (`opt.AddFormatter<TSnapshot>`).
- **Hierarchical Parent-Child Scope Trees**: First-class rendering of execution trees (`▼`, `├─`, `└─`) with self-duration and self-allocated memory breakdown (`ShowHierarchicalTree = true`).
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

### 4. Native Threshold Color Coding & Dynamic Color Selectors

Configure threshold rules or custom lambda delegates to color-code snapshots dynamically:

```csharp
options.AddConsoleSink(opt =>
{
    opt.Colorize = true;

    // Fluent threshold rules (e.g. latency alerts):
    opt.AddThreshold<DurationSnapshot>(
        warn: TimeSpan.FromMilliseconds(50),      // DarkYellow (Orange)
        critical: TimeSpan.FromMilliseconds(100)  // Red
    );

    // Thresholds for memory allocations:
    opt.AddThreshold<MemorySnapshot>(
        warn: 10 * 1024 * 1024,      // 10 MB
        critical: 50 * 1024 * 1024   // 50 MB
    );

    // Or via a custom pattern-matching selector delegate:
    opt.ColorSelector = snapshot => snapshot switch
    {
        DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 100 => ConsoleColor.Red,
        DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 50 => ConsoleColor.DarkYellow,
        ExceptionSnapshot { TotalExceptions: > 0 } => ConsoleColor.Red,
        _ => ConsoleColor.Green
    };
});
```

### 5. Hierarchical Execution Trees, Unit Customization & Custom Formatters

Track correlated parent-child scopes across asynchronous tasks, customize display units, or inject custom snapshot formatters:

```csharp
var hierarchyOptions = new MetricFlowOptions { Topic = "MediaCatalog" }
    .AddHierarchyCounter()
    .AddMemoryCounter()
    .AddThroughputCounter();

hierarchyOptions.AddConsoleSink(opt =>
{
    opt.Colorize = true;
    opt.Prefix = "[Catalog:Trace]";
    opt.ShowHierarchicalTree = true; // Enables tree rendering for root parent operations

    // Unit customization
    opt.DurationUnit = DurationUnit.Auto; // Automatically scales between ms, s, m, h
    opt.MemoryUnit = MemoryUnit.Auto;     // Automatically scales between B, KB, MB, GB
    opt.ThroughputUnit = "epg_items/s";   // Custom throughput rate label

    // Optional: Add custom formatters for specific snapshot types or counter names
    opt.AddFormatter<FailureSnapshot>(f => 
        $"[CUSTOM-FAIL] Operations: {f.TotalOperations}, Failures: {f.TotalFailures}");
});

using var tracker = new MetricTracker(hierarchyOptions);

// Correlates automatically across AsyncLocal execution contexts
using (tracker.Track("GetChannelProgramsUseCase"))
{
    using (var step1 = tracker.Track("MatchSingleChannelUseCase"))
    {
        step1.SetItems(1);
        await Task.Delay(10);
    }

    using (var step2 = tracker.Track("TableStorage.GetProgrammesForChannel"))
    {
        step2.SetItems(1450);
        await Task.Delay(65);

        using (tracker.Track("TableStorage.QuerySegmentAsync"))
        {
            await Task.Delay(25);
        }
    }
}
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
[23:06:59.346] [OrderService:Sink] [Exception:ProcessOrder] Operations: 5, Exceptions: 0 (0.0%)
[23:06:59.348] [OrderService:Sink] [Duration:ProcessOrder] Avg: 12.55 ms, Min: 9.18 ms, Max: 22.92 ms, Total: 62.77 ms
[23:06:59.350] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 150, Operations: 5, Rate: 2389.5 items/s
[23:06:59.351] [OrderService:Sink] [Failure:ProcessOrder] Operations: 5, Failed: 0 (0.0%)
[23:06:59.428] [OrderService:Sink] [Exception:ProcessOrder] Operations: 10, Exceptions: 0 (0.0%)
[23:06:59.428] [OrderService:Sink] [Duration:ProcessOrder] Avg: 11.35 ms, Min: 9.18 ms, Max: 22.92 ms, Total: 113.50 ms
[23:06:59.428] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 550, Operations: 10, Rate: 4845.8 items/s
[23:06:59.428] [OrderService:Sink] [Failure:ProcessOrder] Operations: 10, Failed: 0 (0.0%)

Step 2: Processing slow order (triggers immediately via EmitOnSlowDurationThreshold > 100ms)...
[23:06:59.555] [OrderService:Sink] [Exception:ProcessOrder] Operations: 11, Exceptions: 0 (0.0%)
[23:06:59.555] [OrderService:Sink] [Duration:ProcessOrder] Avg: 21.70 ms, Min: 9.18 ms, Max: 125.19 ms, Total: 238.69 ms
[23:06:59.555] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 551, Operations: 11, Rate: 2308.4 items/s
[23:06:59.555] [OrderService:Sink] [Failure:ProcessOrder] Operations: 11, Failed: 0 (0.0%)

Step 3: Processing order with logical failure (triggers immediately via EmitOnFailure)...
[23:06:59.566] [OrderService:Sink] [Exception:ProcessOrder] Operations: 12, Exceptions: 0 (0.0%)
[23:06:59.566] [OrderService:Sink] [Duration:ProcessOrder] Avg: 20.77 ms, Min: 9.18 ms, Max: 125.19 ms, Total: 249.21 ms, Failed: 1
[23:06:59.566] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 552, Operations: 12, Rate: 2215.0 items/s, Failed: 1
[23:06:59.566] [OrderService:Sink] [Failure:ProcessOrder] Operations: 12, Failed: 1 (8.3%)

Step 4: Processing order that throws an exception (triggers immediately via EmitOnFailure)...
[23:06:59.581] [OrderService:Sink] [Exception:ProcessOrder] Operations: 13, Exceptions: 1 (7.7%)
[23:06:59.581] [OrderService:Sink] [Duration:ProcessOrder] Avg: 20.16 ms, Min: 9.18 ms, Max: 125.19 ms, Total: 262.03 ms, Failed: 2
[23:06:59.581] [OrderService:Sink] [Throughput:ProcessOrder] TotalItems: 553, Operations: 13, Rate: 2110.4 items/s, Failed: 2
[23:06:59.581] [OrderService:Sink] [Failure:ProcessOrder] Operations: 13, Failed: 2 (15.4%)

>>> [Scenario 2] Standalone MetricTracker & Manual Sink Flush <<<

Tracking operations in standalone tracker (no automatic triggers configured)...

>>> [Scenario 3] Native Threshold Color Coding & Dynamic Color Selector <<<

1. Fast payment operation (< 50ms -> Green)...
[23:06:59.655] [Payment:Thresholds] [Exception:AuthorizePayment_Normal] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.656] [Payment:Thresholds] [Duration:AuthorizePayment_Normal] Avg: 20.21 ms, Min: 20.21 ms, Max: 20.21 ms, Total: 20.21 ms

2. Degraded payment operation (50ms - 100ms -> DarkYellow Warning)...
[23:06:59.724] [Payment:Thresholds] [Exception:AuthorizePayment_Normal] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.724] [Payment:Thresholds] [Exception:AuthorizePayment_Degraded] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.724] [Payment:Thresholds] [Duration:AuthorizePayment_Normal] Avg: 20.21 ms, Min: 20.21 ms, Max: 20.21 ms, Total: 20.21 ms
[23:06:59.724] [Payment:Thresholds] [Duration:AuthorizePayment_Degraded] Avg: 65.23 ms, Min: 65.23 ms, Max: 65.23 ms, Total: 65.23 ms

3. Critical latency payment operation (> 100ms -> Red Critical)...
[23:06:59.849] [Payment:Thresholds] [Exception:AuthorizePayment_Critical] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.849] [Payment:Thresholds] [Exception:AuthorizePayment_Normal] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.849] [Payment:Thresholds] [Exception:AuthorizePayment_Degraded] Operations: 1, Exceptions: 0 (0.0%)
[23:06:59.849] [Payment:Thresholds] [Duration:AuthorizePayment_Critical] Avg: 125.25 ms, Min: 125.25 ms, Max: 125.25 ms, Total: 125.25 ms
[23:06:59.849] [Payment:Thresholds] [Duration:AuthorizePayment_Normal] Avg: 20.21 ms, Min: 20.21 ms, Max: 20.21 ms, Total: 20.21 ms
[23:06:59.849] [Payment:Thresholds] [Duration:AuthorizePayment_Degraded] Avg: 65.23 ms, Min: 65.23 ms, Max: 65.23 ms, Total: 65.23 ms

>>> [Scenario 4] Hierarchical Parent-Child Scopes & Custom Colors <<<

Executing parent operation with nested child operations...
[23:07:00.020] [Catalog:Trace] [Exception:MatchSingleChannelUseCase] Operations: 1, Exceptions: 0 (0.0%)
[23:07:00.021] [Catalog:Trace] [Exception:TableStorage.GetProgrammesForChannel] Operations: 1, Exceptions: 0 (0.0%)
[23:07:00.021] [Catalog:Trace] [Exception:TableStorage.QuerySegmentAsync] Operations: 2, Exceptions: 0 (0.0%)
[23:07:00.021] [Catalog:Trace] [Exception:GetChannelProgramsUseCase] Operations: 1, Exceptions: 0 (0.0%)
[23:07:00.020] [Catalog:Trace] [Hierarchy:GetChannelProgramsUseCase] 
▼ [GetChannelProgramsUseCase] (165.75 ms, self: 38.64 ms | alloc: 25.01 KB, self: 9.35 KB)
  ├─ [MatchSingleChannelUseCase] (11.30 ms | items: 1 | alloc: 4.49 KB)
  └─ [TableStorage.GetProgrammesForChannel] (115.81 ms, self: 65.36 ms | items: 1450 | alloc: 11.16 KB, self: 5.67 KB)
      ├─ [TableStorage.QuerySegmentAsync] (25.28 ms | alloc: 5.02 KB)
      └─ [TableStorage.QuerySegmentAsync] (25.17 ms | alloc: 488 B)
[23:07:00.021] [Catalog:Trace] [Duration:MatchSingleChannelUseCase] Avg: 11.30 ms, Min: 11.30 ms, Max: 11.30 ms, Total: 11.30 ms
[23:07:00.021] [Catalog:Trace] [Duration:TableStorage.GetProgrammesForChannel] Avg: 115.81 ms, Min: 115.81 ms, Max: 115.81 ms, Total: 115.81 ms
[23:07:00.021] [Catalog:Trace] [Duration:TableStorage.QuerySegmentAsync] Avg: 25.22 ms, Min: 25.17 ms, Max: 25.28 ms, Total: 50.45 ms
[23:07:00.021] [Catalog:Trace] [Duration:GetChannelProgramsUseCase] Avg: 165.75 ms, Min: 165.75 ms, Max: 165.75 ms, Total: 165.75 ms
[23:07:00.022] [Catalog:Trace] [Throughput:MatchSingleChannelUseCase] TotalItems: 1, Operations: 1, Rate: 88.5 epg_items/s
[23:07:00.022] [Catalog:Trace] [Throughput:TableStorage.GetProgrammesForChannel] TotalItems: 1450, Operations: 1, Rate: 12520.4 epg_items/s
[23:07:00.022] [Catalog:Trace] [Throughput:TableStorage.QuerySegmentAsync] TotalItems: 2, Operations: 2, Rate: 39.6 epg_items/s
[23:07:00.022] [Catalog:Trace] [Throughput:GetChannelProgramsUseCase] TotalItems: 1, Operations: 1, Rate: 6.0 epg_items/s
[23:07:00.024] [Catalog:Trace] [Memory:MatchSingleChannelUseCase] Operations: 1, Avg: 4.46 KB, Total: 4.46 KB
[23:07:00.024] [Catalog:Trace] [Memory:TableStorage.GetProgrammesForChannel] Operations: 1, Avg: 11.17 KB, Total: 11.17 KB
[23:07:00.024] [Catalog:Trace] [Memory:TableStorage.QuerySegmentAsync] Operations: 2, Avg: 2.72 KB, Total: 5.45 KB
[23:07:00.024] [Catalog:Trace] [Memory:GetChannelProgramsUseCase] Operations: 1, Avg: 21.84 KB, Total: 21.84 KB

Structured console log sink example completed successfully!
```
