# MetricFlow

![internal](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-internal.yml/badge.svg)
![public](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-public.yml/badge.svg)
[![DotnetKit.MetricFlow](https://img.shields.io/nuget/v/DotnetKit.MetricFlow)](https://www.nuget.org/packages/DotnetKit.MetricFlow)
[![DotnetKit.MetricFlow.AspNetCore](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.AspNetCore)](https://www.nuget.org/packages/DotnetKit.MetricFlow.AspNetCore)
[![DotnetKit.MetricFlow.OpenTelemetry](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.OpenTelemetry)](https://www.nuget.org/packages/DotnetKit.MetricFlow.OpenTelemetry)

MetricFlow is a lightweight .NET library designed to help developers define and track functional and domain-oriented metrics (such as counters, timers, throughput, and dimensional breakdowns).

## What Can We Do with MetricFlow?

MetricFlow provides domain-oriented observability to **monitor an entire application** or surgically profile **specific portions of your code** (methods, background loops, external calls, batch jobs).

### Streamlined Use Cases

| Use Case | Best For | Registration Style | Primary Injections / APIs | Example Project |
| :--- | :--- | :--- | :--- | :--- |
| **Simple Implementation** | Targeted method/loop profiling, CLI jobs, algorithms | Standalone instantiation | `new MetricTracker(...)` | [BasicConsoleExample](examples/BasicConsoleExample) & [AdvancedConsoleExample](examples/AdvancedConsoleExample) |
| **DI-Based Implementation** | Background workers, daemons, multi-tenant | Standard DI (`IServiceCollection`) | `IMetricTracker`, `[FromKeyedServices]`, `IMetricFlow` | [AdvancedConsoleWithDIExample](examples/AdvancedConsoleWithDIExample) |
| **Web Implementation** | Web APIs, microservices, HTTP routing | ASP.NET Core pipeline | `app.UseMetricFlow()`, `app.MapMetricFlow("/metrics")` | [WebApiExample](examples/WebApiExample) |
| **OpenTelemetry & Observability** | Prometheus, Grafana, Datadog, OTLP collectors, CLI counters | OpenTelemetry SDK / BCL | `.AddMetricFlowInstrumentation()`, `dotnet-counters` | [OpenTelemetryConsoleExample](examples/OpenTelemetryConsoleExample) |

#### 1. Targeted Code Profiling & Standalone Tracking (Simple Implementation)
- **Goal**: Monitor a specific block of code, method, or CLI task with zero ceremony and no DI container.
- **When to use**: Quick diagnostics, utility tools, benchmarks, batch scripts, and targeted algorithms.
- **Key capabilities**: Lightweight `using (tracker.Track("Task"))` or `tracker.TrackAction(...)` measuring duration, memory allocations, throughput, and error rates.
- **Example Projects**: [BasicConsoleExample](examples/BasicConsoleExample) & [AdvancedConsoleExample](examples/AdvancedConsoleExample)

#### 2. Background Daemons, Workers & Multi-Topic Services (DI-Based Implementation)
- **Goal**: Monitor long-running processes, message queues, and modular services through standard Microsoft DI.
- **When to use**: Worker Services, hosted daemons (`IHostedService`), background consumers, and microservices requiring topic isolation.
- **Key capabilities**: `services.AddMetricFlow()`, `options.AddTagsEnricher`, multi-topic fluent builder (`AddMetricTracker`), and native keyed resolution (`[FromKeyedServices("topic")] IMetricTracker`).
- **Example Project**: [AdvancedConsoleWithDIExample](examples/AdvancedConsoleWithDIExample)

#### 3. Full HTTP Request Pipeline & Health Monitoring (Web Implementation)
- **Goal**: Automatically observe incoming HTTP traffic, endpoint performance, and status codes in web APIs.
- **When to use**: REST APIs, Minimal APIs, and web apps needing route-level latency distributions and a standardized telemetry endpoint.
- **Key capabilities**: Turnkey middleware (`app.UseMetricFlow()`), dynamic route resolution, HTTP request tag enrichment (`AddHttpTagsEnricher`), and exposed diagnostic route (`app.MapMetricFlow("/metrics")`).
- **Example Project**: [WebApiExample](examples/WebApiExample)

#### 4. OpenTelemetry & Cloud Telemetry Ecosystem (Observability Implementation)
- **Goal**: Seamlessly export domain metrics and scoped tracking to Prometheus, Grafana, Datadog, AWS CloudWatch, and Azure Monitor via OpenTelemetry or inspect live in the terminal using `dotnet-counters`.
- **When to use**: Microservices connected to centralized APM systems, cloud platforms, and local CLI diagnostics.
- **Key capabilities**: Turnkey `.AddMetricFlowInstrumentation()`, BCL `System.Diagnostics.Metrics` bridge (`Histogram`, `Counter`, `UpDownCounter`), automatic tag cardinality sanitization, and ambient trace correlation (`trace_id`, `span_id`).
- **Example Project**: [OpenTelemetryConsoleExample](examples/OpenTelemetryConsoleExample)

---

## Features

- **Counters**: Track execution counts and occurrences of events.
- **Timers & Duration**: High-precision operation timing via lock-free stopwatch ticks.
- **Throughput & Item Tracking**: Measure batch sizes, entity counts, and processing rates (items/sec) with `ThroughputCounter`.
- **Dimensional Breakdown & Slicing**: Slice and compute operation distributions by business dimensions, tags, or computed rules with `DimensionCounter` and built-in cardinality safeguards.
- **Memory Tracking**: Measure per-operation heap allocations with `MemoryCounter`.
- **Exception & Failure Tracking**: Capture errors, exceptions, and failure counts with `ExceptionCounter`.
- **System.Diagnostics.Metrics Bridge**: Automatic zero-allocation mapping to standard .NET BCL instruments (`Histogram`, `Counter`, `UpDownCounter`) with cardinality protection.
- **OpenTelemetry Integration**: Turnkey `DotnetKit.MetricFlow.OpenTelemetry` package with `.AddMetricFlowInstrumentation()` for exporting to Prometheus, Grafana, Datadog, and OTLP collectors.
- **CLI Diagnostics**: Live real-time inspection in terminal via standard `dotnet-counters monitor`.
- **Metadata and Tags**: Add contextual information to metrics for rich analysis and filtering.
- **Sampling**: Thread-safe sampling control to balance performance and data volume.
- **ASP.NET Core Integration**: Turnkey middleware, endpoint routing resolution, and metric exposition endpoints.
- **Pluggable & Extensible**: Fully customizable counter lifecycle (`CounterBase<TState>`) and trackers (`MetricTrackerBase`).
- **Multi-Targeting**: Native support for `.NET 8.0` and `.NET 10.0`.

## Getting Started

### Prerequisites

- .NET SDK (8.0 or 10.0) installed on your machine

### Installation

Install via NuGet package manager:

```sh
# Core library
dotnet add package DotnetKit.MetricFlow

# OpenTelemetry integration (optional)
dotnet add package DotnetKit.MetricFlow.OpenTelemetry

# ASP.NET Core integration (optional)
dotnet add package DotnetKit.MetricFlow.AspNetCore
```

Or clone and build locally:

```sh
git clone https://github.com/DotnetKit/MetricFlow.git
cd MetricFlow
dotnet restore
dotnet build
```

### Usage

#### 1. Initialize Tracker

Initialize a tracker and optionally chain throughput, memory, exception, and dimension counters:

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("OrderService", new()
    {
        ["environment"] = "production"
    })
    .AddThroughputCounter()
    .AddMemoryCounter()
    .AddExceptionCounter()
    .AddDimensionCounter("country");
```

#### 2. Basic Example (Minimal Setup)

The simplest usage requires zero additional counters or complex configuration—by default, `MetricTracker` records high-precision execution duration:

```csharp
using DotnetKit.MetricFlow;

// Initialize tracker (DurationCounter is included by default)
var tracker = new MetricTracker("BasicConsoleTopic", new()
{
    ["environment"] = "Development"
});

// 1. Scoped tracking with using statement
using (tracker.Track("ProcessOrder"))
{
    await Task.Delay(10);
}

// 2. Delegate tracking with TrackAction
tracker.TrackAction("ValidatePayment", () => Thread.Sleep(5));

// 3. Print formatted telemetry
Console.WriteLine(tracker.ToString());
```

**Output:**
```text
BasicConsoleTopic
Topic Tags:  environment:Development
[Duration] Metric: ProcessOrder
Duration (min, max, avg): 10.50 ms / 12.25 ms / 11.17 ms
Total duration: 55.86 ms

[Duration] Metric: ValidatePayment
Duration (min, max, avg): 5.64 ms / 5.83 ms / 5.70 ms
Total duration: 17.11 ms
```

#### 3. Tracker Capabilities

##### Scope Tracking (`using`)

Measures execution duration until the scope is disposed:

```csharp
// Explicit metric name (with optional tags)
using (tracker.Track("ProcessOrder", new() { ["order_id"] = "123" }))
{
    // work here
}

// Automatic metric name via [CallerMemberName]
void ProcessOrder()
{
    using var _ = tracker.Track(); // Metric name is "ProcessOrder"
}
```

##### Throughput & Batch Tracking (`TrackItems` / `scope.SetItems`)

Track batch or entity processing volume and calculate velocity (`items/sec`):

```csharp
// 1. Specify item count upfront via TrackItems
using (tracker.TrackItems("ImportChannels", 500))
{
    // Process 500 channels...
}

// 2. Or set dynamic count during / at completion of the operation
using (var scope = tracker.Track("IngestMessages"))
{
    var count = await ReadAndProcessBatchAsync();
    scope.SetItems(count); // Records processed count for ThroughputCounter
}

// Inspect results
var throughput = tracker.GetThroughputValues("ImportChannels");
// throughput.TotalItems -> 500
// throughput.ItemsPerSecond -> e.g. 2,500 items/sec
```

##### Dimensional Breakdown (`DimensionCounter` / `AddDimensionCounter`)

Slice and categorize operation counts by business dimensions, tags, composite keys, or custom computed business rules with built-in cardinality safeguards:

```csharp
// 1. Single dimension tag breakdown with cardinality limit (defaults to 250, overflow into [Other])
tracker.AddDimensionCounter("country", maxUniqueValues: 100);

// 2. Composite multi-tag dimension (e.g. "US / CreditCard", "DE / PayPal")
tracker.AddDimensionCounter(
    name: "PaymentChannels",
    dimensionKeys: ["country", "payment_method"]);

// 3. Computed business selector / conditional rules (zero custom metric classes needed)
tracker.AddDimensionCounter("CustomerTier", (tags, metadata) =>
{
    var amount = metadata?.GetValueOrDefault("amount") ?? 0;
    var country = tags?.GetValueOrDefault("country") ?? "Unknown";

    if (amount >= 1000) return $"VIP_{country}";
    if (amount >= 100) return $"Standard_{country}";
    return null; // Return null to skip or mark untracked
});

// Tracking with tags and metadata
using (var scope = tracker.Track("ProcessOrder", new() { ["country"] = "US", ["payment_method"] = "CreditCard" }))
{
    scope.SetMetadata("amount", 1500); // Evaluates CustomerTier to "VIP_US"
}

// Inspect snapshots
var countryDim = tracker.GetDimensionValues("ProcessOrder", "country");
var paymentDim = tracker.GetDimensionValues("ProcessOrder", "PaymentChannels");
var tierDim = tracker.GetDimensionValues("ProcessOrder", "CustomerTier");
```

##### Delegate Tracking (`TrackAction` / `TrackActionAsync`)

Executes an action or task with automatic duration tracking and exception capture:

```csharp
// Explicit metric name (sync or async, with optional return value)
tracker.TrackAction("ProcessOrder", () => DoWork());
var order = await tracker.TrackActionAsync("FetchOrder", async () => await FetchOrderAsync());

// Automatic metric name via [CallerMemberName]
void ProcessOrder()
{
    tracker.TrackAction(() => DoWork()); // Metric name is "ProcessOrder"
}

async Task ProcessOrderAsync()
{
    await tracker.TrackActionAsync(async () => await DoWorkAsync());
}
```

##### Dependency Injection (Console Apps, Workers & Daemons)

Register `MetricFlow` in any .NET application using `Microsoft.Extensions.DependencyInjection` without ASP.NET Core dependencies:

```csharp
using Microsoft.Extensions.DependencyInjection;
using DotnetKit.MetricFlow;

// Register MetricFlow with topic and optional configuration
services.AddMetricFlow("WorkerDaemon", options =>
{
    options.SamplingRate = 1.0;
    options.AddTagsEnricher(tags =>
    {
        tags["env"] = "Production";
    });
});

// Inject IMetricTracker or MetricTracker anywhere in your application
public class QueueWorker(IMetricTracker tracker)
{
    public async Task ProcessAsync()
    {
        using (tracker.Track("ProcessMessage"))
        {
            await HandleMessageAsync();
        }
    }
}
```

##### Multi-Topic Support & Fluent Builder

Register multiple isolated topic trackers in the same application via the fluent builder (`AddMetricTracker`) and resolve them via the `IMetricFlow` facade or native keyed injection:

```csharp
// Fluent builder registration
services.AddMetricFlow("WebApi", options => ...)
    .AddMetricTracker("WeatherRadar", options => ...);

// 1. Resolve via IMetricFlow facade
public class IngestionService(IMetricFlow metricFlow)
{
    public void Run()
    {
        var tracker = metricFlow.GetTracker("WeatherRadar");
        using var scope = tracker.Track("ScanRadar");
    }
}

// 2. Or resolve via native Keyed Services (.NET 8+)
public class RadarWorker([FromKeyedServices("WeatherRadar")] IMetricTracker tracker)
{
    // ...
}
```

##### ASP.NET Core Integration

Enable automated HTTP request duration, memory allocation, and failure tracking via middleware:

```csharp
using DotnetKit.MetricFlow.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register MetricFlow with optional tag enrichment
builder.Services.AddMetricFlow("WebApiExample", options =>
{
    options.EnrichTags = (tags, context) =>
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            tags["tenant_id"] = tenantId!;
        }
    };
});

var app = builder.Build();

// Automated request tracking middleware
app.UseMetricFlow();

// Expose metric snapshot endpoint
app.MapMetricFlow("/metrics");

app.Run();
```

#### 6. OpenTelemetry & Cloud Telemetry (`DotnetKit.MetricFlow.OpenTelemetry`)

MetricFlow seamlessly bridges domain metrics and scoped tracking to the standard OpenTelemetry .NET ecosystem. Simply add `.AddMetricFlowInstrumentation()` to your `MeterProviderBuilder`:

```csharp
using DotnetKit.MetricFlow.OpenTelemetry;
using OpenTelemetry.Metrics;

services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            // Subscribe to all MetricFlow topics ("DotnetKit.MetricFlow.*")
            .AddMetricFlowInstrumentation(options =>
            {
                options.MeterNamePattern = "DotnetKit.MetricFlow.*";
                options.RecordActiveOperations = true;
            })
            // Export to any OpenTelemetry collector
            .AddOtlpExporter()
            .AddPrometheusExporter();
    });
```

##### Standard BCL Instruments Mapped
| MetricFlow Concept | Instrument Type | Metric Name | Unit | Tags / Attributes |
|---|---|---|---|---|
| **DurationCounter** | `Histogram<double>` | `{metricName}.duration` | `ms` | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Execution Counts** | `Counter<long>` | `{metricName}.total` | `{operations}` | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Throughput / Items**| `Counter<long>` | `{metricName}.items` | `{items}` | `operation`, sanitized tags |
| **ExceptionCounter** | `Counter<long>` | `{metricName}.exceptions` | `{exceptions}` | `operation`, `exception.type`, sanitized tags |
| **In-Flight / Concurrency** | `UpDownCounter<long>` | `{metricName}.active` | `{operations}` | `operation`, sanitized tags |

##### Ambient Distributed Tracing Correlation
Enrich scope tags with the ambient OpenTelemetry `Activity.Current` trace and span IDs:

```csharp
using DotnetKit.MetricFlow.OpenTelemetry;

var tags = new Dictionary<string, string> { ["region"] = "eu" }.WithTraceContext();
using (tracker.Track("ProcessOrder", tags))
{
    // Metric measurements now carry trace_id and span_id attributes
}
```

#### 7. Live Terminal Diagnostics (`dotnet-counters`)

Because MetricFlow is backed directly by `System.Diagnostics.Metrics.Meter`, you can inspect active metrics in real time in your terminal without configuring any external collector:

```sh
# Monitor live operations for topic "OrderProcessingService"
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow.OrderProcessingService

# Or monitor across all MetricFlow topics
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow
```

---

### Examples

- **[BasicConsoleExample](examples/BasicConsoleExample)**: Simplest implementation demonstrating minimal tracker setup and duration measurement with zero optional counters.
- **[AdvancedConsoleExample](examples/AdvancedConsoleExample)**: Full multi-counter demonstration including duration, throughput (items/sec and batch sizing), memory allocation, exceptions, and delegate tracking.
- **[AdvancedConsoleWithDIExample](examples/AdvancedConsoleWithDIExample)**: Standard Microsoft DI integration demonstrating fluent builder (`AddMetricTracker`), `AddTagsEnricher`, multi-topic tracking, keyed services (`[FromKeyedServices]`), worker pipelines, and programmatic telemetry queries.
- **[OpenTelemetryConsoleExample](examples/OpenTelemetryConsoleExample)**: Complete OpenTelemetry integration demonstrating `.AddMetricFlowInstrumentation()`, raw console metric export, cardinality protection, batch items throughput, and trace correlation.
- **[WebApiExample](examples/WebApiExample)**: Demonstrates ASP.NET Core integration, middleware, and `/metrics` endpoint.
- **[CustomCounters](examples/CustomCounters)**: Demonstrates extension capabilities by implementing custom counters and trackers.

Run the examples:

```sh
# Basic console example (minimal setup)
dotnet run --project examples/BasicConsoleExample

# Multi-counter console example (advanced: duration, throughput, memory, exceptions)
dotnet run --project examples/AdvancedConsoleExample

# Dependency injection console example (DI, AddTagsEnricher, keyed services)
dotnet run --project examples/AdvancedConsoleWithDIExample

# OpenTelemetry console example (OpenTelemetry SDK, BCL bridge, console exporter)
dotnet run --project examples/OpenTelemetryConsoleExample

# ASP.NET Core Web API example
dotnet run --project examples/WebApiExample
```

#### Extension Capabilities (Custom Counters & Trackers)

MetricFlow is designed to be extensible. You can implement custom counters by deriving from `CounterBase<TState>` and pre-configure trackers by inheriting from `MetricTrackerBase`.

See the examples in [`examples/CustomCounters`](examples/CustomCounters):

##### 1. Custom Counter (`UtcDurationCounter`)

Inherit from `CounterBase<TState>` to track state during operation lifecycle (`OnIn` / `OnOut`):

```csharp
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace CustomCounters;

/// <summary>
/// Counter based on UTC time using state token.
/// </summary>
public class UtcDurationCounter(string name = "UtcDuration") : CounterBase<long>(name)
{
    private readonly DurationCounter _inner = new(name);

    public override long OnIn(in InContext context)
    {
        if (!IsEnabled) return 0;
        return DateTimeOffset.UtcNow.Ticks;
    }

    public override void OnOut(long state, in OutContext context)
    {
        if (!IsEnabled) return;
        TimeSpan elapsed = TimeSpan.Zero;
        if (state > 0)
        {
            elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - state);
        }
        _inner.OnOut(state, new OutContext(context.MetricName, context.Failed, context.Exception, elapsed, context.Tags, context.Metadata, context.UtcTimestamp));
    }

    public override IMetricSnapshot? GetSnapshot(string metricName) => _inner.GetSnapshot(metricName);
    public override IEnumerable<IMetricSnapshot> GetAllSnapshots() => _inner.GetAllSnapshots();
    public override void Reset() => _inner.Reset();
}
```

##### 2. Custom Tracker (`CustomMetricTrackerWithUtcCounter`)

Inherit from `MetricTrackerBase` to provide a domain-specific or pre-configured tracker with custom counters:

```csharp
using DotnetKit.MetricFlow.Abstractions;

namespace CustomCounters;

/// <summary>
/// Custom metric tracker implementation with default UtcDurationCounter
/// </summary>
public class CustomMetricTrackerWithUtcCounter : MetricTrackerBase
{
    public CustomMetricTrackerWithUtcCounter(
        string topic,
        IReadOnlyDictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0)
        : base(topic, topicTags, samplingRate)
    {
        RegisterCounter(new UtcDurationCounter());
    }
}
```

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the development roadmap, upcoming milestones, and architectural improvements.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes, releases, and fixes.
