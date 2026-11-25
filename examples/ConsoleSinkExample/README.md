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
- **Automatic Parent-Child Tree & Table Views**: Indented parent-child execution trees (`▼`, `├─`, `└─` via `DisplayMode = ConsoleDisplayMode.Tree`) and tables (`ConsoleDisplayMode.Table`) correlated automatically via `AsyncLocal` technical metadata.
- **Execution-Lifecycle Sampling Triggers**:
  - **Stride Sampling (`EmitEveryNExecutions`)**: Automatically emits snapshots every $N$ executions (e.g., every 5 or 100 operations).
  - **Latency / Slow-Operation Sampling (`EmitOnSlowDurationThreshold`)**: Immediately emits snapshots when an operation exceeds a duration threshold (e.g., > 100ms).
  - **Tail / Outlier Sampling (`EmitOnFailure`)**: Immediately emits snapshots when an operation fails logically (`SetFailed(true)`) or throws an unhandled exception.
  - **Probabilistic Sampling (`EmitSampleRate`)**: Emits snapshots based on a random percentage.
- **Standalone & Fluent Chaining**: Seamless configuration directly on standalone `MetricTracker` via `.AddConsoleSink(...)` and `.ConfigureSinkSampling(...)`, or via DI options.
- **Manual / Graceful Shutdown Flush**: Explicitly flush all pending snapshots via `tracker.FlushSinks()` or `await tracker.FlushSinksAsync()`.

---

## Code Walkthrough

### 1. Standalone Tracker & Trigger Configuration

Register `ConsoleMetricSink` and configure lifecycle triggers directly on `MetricTracker`:

```csharp
using var orderTracker = new MetricTracker("OrderService", new()
{
    ["environment"] = "Production",
    ["region"] = "us-east-1"
})
.AddThroughputCounter()
.AddFailureCounter()
// 1. Configure the ConsoleMetricSink directly on the tracker
.AddConsoleSink(opt =>
{
    opt.Colorize = true;                 // ANSI color formatting
    opt.IncludeTimestamp = true;         // [HH:mm:ss.fff] timestamp prefix
    opt.TimestampFormat = "HH:mm:ss.fff";
    opt.Prefix = "[OrderService:Sink]";  // Custom log line prefix
})
// 2. Configure timer-free sampling triggers
.ConfigureSinkSampling(trig =>
{
    trig.EmitEveryNExecutions = 5;                              // Stride: every 5 executions
    trig.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100); // Latency: > 100ms
    trig.EmitOnFailure = true;                                  // Tail: on errors / exceptions
});

var orderService = new OrderFulfillmentService(orderTracker);
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
    .AddTelemetryCounters(includeMemory: true)
    .AddThroughputCounter();

hierarchyOptions.AddConsoleSink(opt =>
{
    opt.Colorize = true;
    opt.Prefix = "[Catalog:Trace]";
    opt.DisplayMode = ConsoleDisplayMode.Tree; // Renders visual parent-child execution tree (▼, ├─, └─)

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
    using (var step1 = tracker.Track("Adapter.MatchSingleChannelUseCase"))
    {
        step1.SetItems(1);
        await Task.Delay(10);
    }

    using (var step2 = tracker.Track("Adapter.GetProgrammesForChannel"))
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

### Sample Output (flat)

```text
================================================================
        MetricFlow - Structured Console Log Sink Example
================================================================

>>> [Scenario 1] Standalone MetricTracker & Timer-Free Lifecycle Triggers <<<

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
[21:50:33.800] [Catalog:Trace]
▼ [GetChannelProgramsUseCase] (159.74 ms, self: 30.88 ms | items: 1 | alloc: 50.42 KB, self: 24.26 KB)
  ├─ [Adapter.MatchSingleChannelUseCase] (11.26 ms | items: 1 | alloc: 4.68 KB)
  └─ [Adapter.GetProgrammesForChannel] (117.60 ms, self: 66.24 ms | items: 1,450 | alloc: 21.48 KB, self: 15.60 KB)
      └─ [TableStorage.QuerySegmentAsync] x2 (51.35 ms, avg: 25.68 ms | items: 2 | alloc: 5.88 KB)

Structured console log sink example completed successfully!
```
