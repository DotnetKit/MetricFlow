# Logger Sink Example with Serilog (`LoggerSinkExample`)

This example demonstrates the structured **Logger Sink** (`LoggerMetricSink`) using the standard Microsoft `ILogger` abstraction integrated with **Serilog** and MetricFlow's **Timer-Free Lifecycle Triggers** (`MetricSinkTriggerOptions`).

---

## Overview

`LoggerMetricSink` bridges MetricFlow's high-performance in-memory metric snapshots directly into standard .NET `ILogger` pipelines. When combined with modern logging frameworks such as **Serilog**, **NLog**, or Microsoft's default logging providers:
- Metric snapshots are logged as structured log events with first-class properties (e.g., `CounterName`, `MetricName`, `TotalItems`, `Operations`, `Rate`).
- Normal operations are logged at a configurable level (`LogLevel.Information` by default).
- Operations encountering logical failures (`scope.SetFailed(true)`) or unhandled exceptions are automatically promoted to a higher severity level (`LogLevel.Warning` or `LogLevel.Error`).
- Snapshots are dispatched using **execution-lifecycle triggers** (stride, latency thresholds, failure/exception) during operation completion (`Track` disposal or `Out`), completely avoiding background polling threads or timers.

---

## Key Features

- **Standard `ILogger` Abstraction**: Works seamlessly with any logging provider supported by Microsoft.Extensions.Logging (Serilog, NLog, Console, Application Insights, Seq, Elasticsearch, AWS CloudWatch, Datadog).
- **Structured Message Templates**: Emits events using standard message templates that preserve property names for structured search and filtering in log aggregators.
- **Dynamic Log Level Elevation**:
  - `LogLevel` (default: `Information`): Used for routine metric snapshots.
  - `FailureLogLevel` (default: `Warning`): Automatically applied when a snapshot reports failed operations.
  - `LogLevelSelector`: Optional delegate for dynamic, custom log-level selection.
- **Timer-Free Sampling Triggers**:
  - **Stride Sampling (`EmitEveryNExecutions`)**: Dispatches snapshots every $N$ executions.
  - **Tail / Outlier Sampling (`EmitOnFailure`)**: Immediately dispatches snapshots when an operation fails.
  - **Latency Sampling (`EmitOnSlowDurationThreshold`)**: Immediately dispatches snapshots when operation latency exceeds a threshold.
- **Flexible DI & Standalone Integration**:
  - Automatically resolves `ILoggerFactory` / `ILogger` from the DI container.
  - Can also be directly instantiated with any `ILogger` or `ILoggerFactory`.
- **Clean Formatting**: Single-line log output with optional prefix and customizable timestamp formatting.

---

## Code Walkthrough

### 1. Serilog & Dependency Injection Configuration

Configure Serilog and register `LoggerMetricSink` with `AddMetricFlow`:

```csharp
// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var services = new ServiceCollection();

// 2. Register Serilog as the ILogger provider
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(dispose: true);
});

// 3. Register MetricFlow with LoggerMetricSink
services.AddMetricFlow("PaymentService", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["environment"] = "Production";
        tags["gateway"] = "Stripe";
    })
    .AddThroughputCounter()
    .AddFailureCounter();

    // Configure LoggerMetricSink
    options.AddLoggerSink(opt =>
    {
        opt.CategoryName = "PaymentService.Telemetry";
        opt.LogLevel = LogLevel.Information;       // Normal metrics -> Information
        opt.FailureLogLevel = LogLevel.Warning;    // Failures -> Warning
        opt.Prefix = "[PaymentService]";
    });

    // Configure lifecycle sampling triggers
    options.ConfigureSinkTriggers(trig =>
    {
        trig.EmitEveryNExecutions = 5;                              // Stride: every 5 executions
        trig.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100); // Latency: > 100ms
        trig.EmitOnFailure = true;                                  // Tail: on errors / exceptions
    });
});

services.AddTransient<PaymentProcessingService>();
await using var provider = services.BuildServiceProvider();
```

### 2. Operational Workflows & Automatic Triggers

When worker services execute operations, triggers dispatch logs directly through Serilog:

```csharp
// Stride trigger: Emits at Information level every 5 operations
for (int i = 1; i <= 10; i++)
{
    using (_tracker.TrackItems("AuthorizePayment", itemCount: i * 5))
    {
        await Task.Delay(10);
    }
}

// Latency trigger: Emits at Information level immediately when duration > 100ms
using (_tracker.Track("AuthorizePayment"))
{
    await Task.Delay(120);
}

// Failure trigger: Emits at Warning level immediately on logical failure
using (var scope = _tracker.Track("AuthorizePayment"))
{
    await Task.Delay(10);
    scope.SetFailed(true);
}

// Exception trigger: Emits at Warning level immediately on unhandled exception
try
{
    await _tracker.TrackActionAsync("AuthorizePayment", async () =>
    {
        throw new TimeoutException("Gateway connection timeout");
    });
}
catch (TimeoutException) { }
```

### 3. Standalone Tracker with Direct `ILogger`

You can also use `LoggerMetricSink` in standalone mode without a full DI container:

```csharp
var options = new MetricFlowOptions { Topic = "PayoutService" }
    .AddThroughputCounter()
    .AddFailureCounter();

options.AddLoggerSink(myLogger, opt =>
{
    opt.LogLevel = LogLevel.Information;
    opt.Prefix = "[PayoutService]";
});

using var tracker = new MetricTracker(options);

using (tracker.TrackItems("ExecutePayout", itemCount: 25))
{
    await Task.Delay(15);
}

// Manually flush all registered sinks at shutdown
tracker.FlushSinks();
```

---

## Running the Example

Run the project directly from the repository root:

```bash
dotnet run --project examples/LoggerSinkExample/LoggerSinkExample.csproj
```

### Sample Output

```text
================================================================
    MetricFlow - Structured Logger Sink (Serilog) Example       
================================================================

Step 1: Processing 10 successful payments (triggers every 5 executions via EmitEveryNExecutions)...
[22:18:04.893 INF] [PaymentService] [Duration:AuthorizePayment] [Duration] Metric: AuthorizePayment | Duration (min, max, avg): 11.04 ms / 13.80 ms / 11.62 ms | Total duration: 58.10 ms
[22:18:04.906 INF] [PaymentService] [Throughput:AuthorizePayment] TotalItems: 75, Operations: 5, Rate: 1290.8 items/s
[22:18:04.907 INF] [PaymentService] [Failure:AuthorizePayment] Operations: 5, Failed: 0 (0.0%)
[22:18:04.963 INF] [PaymentService] [Duration:AuthorizePayment] [Duration] Metric: AuthorizePayment | Duration (min, max, avg): 11.03 ms / 13.80 ms / 11.34 ms | Total duration: 113.44 ms
[22:18:04.963 INF] [PaymentService] [Throughput:AuthorizePayment] TotalItems: 275, Operations: 10, Rate: 2424.2 items/s
[22:18:04.963 INF] [PaymentService] [Failure:AuthorizePayment] Operations: 10, Failed: 0 (0.0%)

Step 2: Processing slow payment (triggers immediately via EmitOnSlowDurationThreshold > 100ms)...
[22:18:05.084 INF] [PaymentService] [Duration:AuthorizePayment] [Duration] Metric: AuthorizePayment | Duration (min, max, avg): 11.03 ms / 121.12 ms / 21.32 ms | Total duration: 234.56 ms
[22:18:05.084 INF] [PaymentService] [Throughput:AuthorizePayment] TotalItems: 276, Operations: 11, Rate: 1176.7 items/s
[22:18:05.084 INF] [PaymentService] [Failure:AuthorizePayment] Operations: 11, Failed: 0 (0.0%)

Step 3: Processing payment with logical decline (triggers immediately at Warning level via EmitOnFailure)...
[22:18:05.096 INF] [PaymentService] [Duration:AuthorizePayment] [Duration] Metric: AuthorizePayment | Duration (min, max, avg): 11.03 ms / 121.12 ms / 20.48 ms | Total duration: 245.75 ms
[22:18:05.096 WRN] [PaymentService] [Throughput:AuthorizePayment] TotalItems: 277, Operations: 12, Rate: 1127.2 items/s, Failed: 1
[22:18:05.096 WRN] [PaymentService] [Failure:AuthorizePayment] Operations: 12, Failed: 1 (8.3%)

Step 4: Processing payment encountering a network exception (triggers immediately via EmitOnFailure)...
[22:18:05.109 INF] [PaymentService] [Exception:AuthorizePayment] [Exception] Metric: AuthorizePayment | Exceptions / Operations: 1 / 13 (7.69%) | Exceptions Breakdown: |   - TimeoutException: 1
[22:18:05.109 INF] [PaymentService] [Duration:AuthorizePayment] [Duration] Metric: AuthorizePayment | Duration (min, max, avg): 11.03 ms / 121.12 ms / 19.81 ms | Total duration: 257.50 ms
[22:18:05.109 WRN] [PaymentService] [Throughput:AuthorizePayment] TotalItems: 278, Operations: 13, Rate: 1079.6 items/s, Failed: 2
[22:18:05.109 WRN] [PaymentService] [Failure:AuthorizePayment] Operations: 13, Failed: 2 (15.4%)

>>> [Scenario 2] Standalone MetricTracker with Direct ILogger & Manual Flush <<<

Tracking payout operations in standalone tracker...
Manually flushing sinks at shutdown via FlushSinks()...

[22:18:05.158 INF] [PayoutService] [Duration:ExecutePayout] [Duration] Metric: ExecutePayout | Duration (min, max, avg): 15.06 ms / 15.95 ms / 15.45 ms | Total duration: 46.36 ms
[22:18:05.158 INF] [PayoutService] [Throughput:ExecutePayout] TotalItems: 60, Operations: 3, Rate: 1294.2 items/s
[22:18:05.158 INF] [PayoutService] [Failure:ExecutePayout] Operations: 3, Failed: 0 (0.0%)

Logger sink (Serilog) example completed successfully!
```
