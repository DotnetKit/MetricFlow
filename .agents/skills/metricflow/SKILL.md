---
name: metricflow
description: >-
  Guides developers and AI agents on integrating DotnetKit.MetricFlow into .NET projects (ASP.NET Core,
  Worker Services, Console applications, and OpenTelemetry). Use this skill when installing MetricFlow,
  configuring dependency injection, instrumenting code with counters (duration, throughput, memory,
  dimension, hierarchy, exception), configuring sinks (Console, ILogger/Serilog), or exporting metrics.
---

# MetricFlow Integration Skill

A comprehensive guide for integrating **DotnetKit.MetricFlow** into .NET 8.0+ and .NET 10.0+ projects. MetricFlow provides domain-oriented observability, high-precision operation profiling, and native BCL/OpenTelemetry instrumentation with zero lock contention on hot paths.

---

## 1. Package Selection & Installation

Install the required NuGet packages based on your project requirements:

```sh
# Core tracking engine, counters, DI extensions, and structured sinks
dotnet add package DotnetKit.MetricFlow

# Turnkey ASP.NET Core middleware, HTTP routing tags, and /metrics endpoint
dotnet add package DotnetKit.MetricFlow.AspNetCore

# OpenTelemetry meter provider instrumentation and distributed trace correlation
dotnet add package DotnetKit.MetricFlow.OpenTelemetry
```

| Package | When to Use |
| :--- | :--- |
| `DotnetKit.MetricFlow` | **Always required**. Core library, standalone trackers, DI, console/logger sinks. |
| `DotnetKit.MetricFlow.AspNetCore` | Use in ASP.NET Core Web APIs, Minimal APIs, and MVC apps. |
| `DotnetKit.MetricFlow.OpenTelemetry` | Use when exporting telemetry to Prometheus, Grafana, Datadog, or OTLP collectors. |

---

## 2. Choosing the Integration Architecture

```
                  ┌─────────────────────────────┐
                  │ What type of application?   │
                  └──────────────┬──────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         ▼                       ▼                       ▼
  [ASP.NET Core API]      [Worker / Daemon]     [CLI / Micro-benchmark]
         │                       │                       │
Use Web DI + Middleware  Use Host DI Builder    Use Standalone Tracker
 services.AddMetricFlow() services.AddMetricFlow() new MetricTracker(...)
 app.UseMetricFlow()      Inject IMetricTracker
 app.MapMetricFlow()
```

---

## 3. Configuration & Dependency Injection Recipes

### Recipe A: ASP.NET Core Web API (Minimal API or Controllers)

Registers MetricFlow, automatically captures HTTP endpoint metrics via middleware, and exposes `/metrics`.

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Register MetricFlow with custom HTTP tag enrichment
builder.Services.AddMetricFlow("WebApi", options =>
{
    // Enrich every request with ambient HTTP context (e.g., Tenant, Client IP)
    options.AddHttpTagsEnricher((tags, httpContext) =>
    {
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            tags["tenant_id"] = tenantId!;
        }
    });

    // Register desired optional counters
    options.AddThroughputCounter()
           .AddMemoryCounter()
           .AddExceptionCounter()
           .AddHierarchyCounter();
});

var app = builder.Build();

// 2. Add automated HTTP request profiling middleware
app.UseMetricFlow();

// 3. Expose JSON metrics snapshot endpoint
app.MapMetricFlow("/metrics");

// 4. Inject IMetricTracker into endpoints or controllers
app.MapGet("/orders/{id}", async (string id, IMetricTracker tracker) =>
{
    using var scope = tracker.Track("GetOrderById", new Dictionary<string, string> { ["order_id"] = id });
    return Results.Ok(await FetchOrderAsync(id));
});

app.Run();
```

---

### Recipe B: Background Workers, Daemons & Generic Host

Uses standard Microsoft Dependency Injection without ASP.NET Core dependencies.

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMetricFlow("QueueWorker", options =>
        {
            options.AddTagsEnricher(tags =>
            {
                tags["environment"] = context.HostingEnvironment.EnvironmentName;
                tags["machine"] = Environment.MachineName;
            })
            .AddThroughputCounter()
            .AddMemoryCounter()
            .AddFailureCounter();
        });

        services.AddHostedService<MessageConsumerService>();
    })
    .Build();

await host.RunAsync();

// Service class injecting IMetricTracker
public class MessageConsumerService(IMetricTracker tracker) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = tracker.Track("ConsumeBatch"))
            {
                var processedCount = await ProcessNextBatchAsync(stoppingToken);
                scope.SetItems(processedCount); // Calculates throughput rate (items/sec)
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

---

### Recipe C: Multi-Topic Trackers & Keyed Injection

For isolating metrics across distinct domain sub-systems within the same process.

```csharp
services.AddMetricFlow("BillingTopic", options =>
{
    options.AddThroughputCounter();
})
// Register secondary topic tracker using fluent builder
.AddMetricTracker("InventoryTopic", options =>
{
    options.AddMemoryCounter();
});

// Method 1: Resolve specific topic via IMetricFlow facade
public class IngestionHandler(IMetricFlow metricFlow)
{
    public void Handle()
    {
        var inventoryTracker = metricFlow.GetTracker("InventoryTopic");
        using var scope = inventoryTracker.Track("SyncStock");
    }
}

// Method 2: Inject directly using native .NET 8+ Keyed Services
public class BillingProcessor([FromKeyedServices("BillingTopic")] IMetricTracker billingTracker)
{
    public void Process()
    {
        using var scope = billingTracker.Track("ChargeCard");
    }
}
```

---

### Recipe D: Standalone Console or Micro-Benchmark (No DI)

Ideal for algorithms, benchmark utilities, and CLI scripts:

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("ImportJob", new Dictionary<string, string>
{
    ["run_id"] = Guid.NewGuid().ToString("N")
})
.AddThroughputCounter()
.AddMemoryCounter()
.AddExceptionCounter();

// Measure execution
using (tracker.TrackItems("BatchProcess", 500))
{
    await DoHeavyWorkAsync();
}

// Print formatted summary report
Console.WriteLine(tracker.ToString());
```

---

## 4. Code Instrumentation Patterns & Best Practices

### 1. Scoped Tracking (`tracker.Track`)
Supports automatic operation naming via C# `[CallerMemberName]`:

```csharp
// Explicit operation name
using (tracker.Track("ProcessPayment"))
{
    await ProcessPaymentAsync();
}

// Automatic operation name ("ValidateUser" inferred from enclosing method)
public async Task ValidateUser()
{
    using var scope = tracker.Track();
    await Task.Yield();
}

// Scoped tracking with tags
using (tracker.Track("ExecuteQuery", new Dictionary<string, string> { ["query_type"] = "SELECT" }))
{
    // query work
}
```

---

### 2. Throughput & Batch Volume (`TrackItems` / `scope.SetItems`)
Measures batch counts and calculates processing rate (`items/sec`):

```csharp
// Upfront batch size:
using (tracker.TrackItems("IngestRecords", batchSize: records.Count))
{
    await BulkInsertAsync(records);
}

// Dynamic item count resolved during operation:
using (var scope = tracker.Track("ExportData"))
{
    var exportedCount = await StreamToTargetAsync();
    scope.SetItems(exportedCount);
}

// Inspect results programmatically
var throughput = tracker.GetThroughputSnapshot("IngestRecords");
// throughput.ItemsPerSecond, throughput.TotalItems, throughput.AverageItemsPerOperation
```

---

### 3. Asynchronous Stream Tracking (`TrackStream`)
Automatically tracks execution duration and yields items count for `IAsyncEnumerable<T>` and `IEnumerable<T>`:

```csharp
using DotnetKit.MetricFlow;

// In a repository or service streaming items
public async IAsyncEnumerable<Product> StreamProductsAsync([EnumeratorCancellation] CancellationToken ct = default)
{
    var rawStream = _dbClient.QueryAllAsync(ct);
    
    // Tracks total stream duration and item count under operation "StreamProducts"
    await foreach (var item in rawStream.TrackStream(tracker, "StreamProducts", ct))
    {
        yield return item;
    }
}
```

---

### 4. Hierarchical Call Trees & Self-Time (`HierarchyCounter`)
Correlates nested execution scopes across asynchronous flows (`AsyncLocal`) and computes exclusive self-duration and memory allocation:

```csharp
// Enable hierarchy counter during registration
tracker.AddHierarchyCounter();

// Nested execution scopes
using (tracker.Track("ParentWorkflow"))
{
    using (tracker.Track("Step1_FetchData"))
    {
        await Task.Delay(20);
    }

    using (tracker.Track("Step2_Compute"))
    {
        await Task.Delay(50);
    }
}

// Render formatted call tree
var tree = tracker.GetHierarchySnapshot("ParentWorkflow");
Console.WriteLine(tree?.ToFormattedString());
/*
Output:
▼ [ParentWorkflow] (72.50 ms, self: 2.50 ms)
  ├─ [Step1_FetchData] (20.00 ms)
  └─ [Step2_Compute] (50.00 ms)
*/
```

---

### 5. Business Dimensions & Cardinality Protection (`DimensionCounter`)
Enables categorical slicing of metrics with built-in cardinality limits (default: 250 unique values per dimension, overflow routed to `[Other]`):

```csharp
// 1. Single dimension tag
tracker.AddDimensionCounter("country", maxUniqueValues: 50);

// 2. Composite multi-tag dimension (e.g. "US / Visa", "DE / PayPal")
tracker.AddDimensionCounter("PaymentBreakdown", ["country", "payment_method"]);

// 3. Dynamic computed rule
tracker.AddDimensionCounter("CustomerTier", (tags, metadata) =>
{
    var amount = metadata?.GetValueOrDefault("amount") ?? 0;
    return amount switch
    {
        >= 1000 => "VIP",
        >= 100  => "Standard",
        _       => "Basic"
    };
});

// Triggering the rule
using (var scope = tracker.Track("Checkout", new() { ["country"] = "US" }))
{
    scope.SetMetadata("amount", 1250); // Categorized as "VIP"
}
```

---

## 5. Observability, Sinks & OpenTelemetry

### 1. OpenTelemetry Export (Prometheus, OTLP, Datadog)

Wire OpenTelemetry directly in your MetricFlow setup using the fluent builder:

```csharp
using DotnetKit.MetricFlow.OpenTelemetry;
using OpenTelemetry.Metrics;

services.AddMetricFlow("OrderSystem", options =>
{
    options.AddThroughputCounter()
           .AddExceptionCounter();
})
.WithOpenTelemetry(otel =>
{
    otel.WithMetrics(metrics =>
    {
        metrics.AddOtlpExporter()
               .AddPrometheusExporter();
    });
    otel.WithTracing();
});
```

Or attach to an existing `services.AddOpenTelemetry()` registration:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMetricFlowInstrumentation(options =>
        {
            options.Topics.Add("OrderSystem"); // Target specific topics or leave empty for all
        })
        .AddOtlpExporter();
    });
```

### 2. Ambient Distributed Trace Correlation
Attach active OpenTelemetry `Activity.Current` trace and span IDs to metrics:

```csharp
using DotnetKit.MetricFlow.OpenTelemetry;

var tags = new Dictionary<string, string> { ["tenant"] = "t1" }.WithTraceContext();
using (tracker.Track("ProcessItem", tags))
{
    // Measurements will include active trace_id and span_id attributes
}
```

### 3. Structured Real-Time Sinks & Timer-Free Sampling

MetricFlow includes high-performance console and logger sinks driven by operation lifecycle triggers rather than CPU-burning background polling timers.

```csharp
services.AddMetricFlow("OrderService", options =>
{
    // A. Structured ANSI Console Sink
    options.AddConsoleSink(opt =>
    {
        opt.Colorize = true;
        opt.TimestampFormat = "HH:mm:ss.fff";
        opt.Prefix = "[OrderService]";
    });

    // B. Structured ILogger / Serilog Sink
    options.AddLoggerSink(opt =>
    {
        opt.CategoryName = "OrderService.Metrics";
        opt.LogLevel = LogLevel.Information;
        opt.FailureLogLevel = LogLevel.Warning; // Automatically elevated on exceptions
    });

    // C. Execution-Lifecycle Sampling Triggers (Timer-Free)
    options.ConfigureSinkSampling(sampling =>
    {
        sampling.EmitEveryNExecutions = 10;                     // Stride: every 10 completions
        sampling.EmitOnFailure = true;                          // Outlier: immediately on failure
        sampling.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(200); // Latency spike trigger
    });
});
```

---

## 6. Live CLI Inspection (`dotnet-counters`)

Because MetricFlow instruments standard .NET BCL `System.Diagnostics.Metrics.Meter` instances, you can inspect live applications in real time without configuring any external collector:

```sh
# Monitor live counters for a specific topic:
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow.<TopicName>

# Monitor all MetricFlow topics across the runtime:
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow
```

---

## 7. Integration Checklist & Guardrails

- [ ] **Cardinality Guard**: Never use high-cardinality values (e.g. `order_id`, `user_id`, `guid`, raw URLs with IDs) as dimension keys. Use tags or metadata instead, or rely on `maxUniqueValues` safeguards.
- [ ] **DI Lifetime**: Register `IMetricTracker` as a **Singleton**. `MetricTracker` instances are completely thread-safe and lock-free; recreating them per request incurs allocation overhead and loses historical telemetry.
- [ ] **CallerMemberName Idiom**: For clean code, omit the operation name in `tracker.Track()` when instrumenting the entire body of a method.
- [ ] **Disposal**: Always scope tracking with `using (tracker.Track(...))` or `using var _ = tracker.Track(...)` to guarantee that operation completion (`OnOut`) executes even if exceptions are thrown.
- [ ] **Exception Preservation**: `tracker.TrackAction` rethrows captured exceptions after recording them in `ExceptionCounter` and `FailureCounter`.
