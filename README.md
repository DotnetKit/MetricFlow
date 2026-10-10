# MetricFlow

![internal](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-internal.yml/badge.svg)
![public](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-public.yml/badge.svg)
[![DotnetKit.MetricFlow](https://img.shields.io/nuget/v/DotnetKit.MetricFlow)](https://www.nuget.org/packages/DotnetKit.MetricFlow)
[![DotnetKit.MetricFlow.AspNetCore](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.AspNetCore)](https://www.nuget.org/packages/DotnetKit.MetricFlow.AspNetCore)
[![DotnetKit.MetricFlow.OpenTelemetry](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.OpenTelemetry)](https://www.nuget.org/packages/DotnetKit.MetricFlow.OpenTelemetry)

MetricFlow is a lightweight and standalone .NET library designed to simplify the way developers define and track technical and business related metrics (such as counters, timers, throughput and dimensional breakdowns).

## What Can We Do with MetricFlow?

MetricFlow provides domain-oriented observability to **monitor an entire application** or surgically profile **specific portions of your code** (methods, background loops, external calls, batch jobs).

It could be used to create an in-memory metrics store with support for OpenTelemetry integration.
It can be used in both ASP.NET Core and non-ASP.NET Core applications.

### Streamlined Use Cases

| Use Case                          | Best For                                                    | Registration Style                 | Primary Injections / APIs                              | Example Project                                                                                                 |
| :-------------------------------- | :---------------------------------------------------------- | :--------------------------------- | :----------------------------------------------------- | :-------------------------------------------------------------------------------------------------------------- |
| **Simple Implementation**         | Targeted method/loop profiling, CLI jobs, algorithms        | Standalone instantiation           | `new MetricTracker(...)`                               | [BasicConsoleExample](examples/BasicConsoleExample) & [AdvancedConsoleExample](examples/AdvancedConsoleExample) |
| **DI-Based Implementation**       | Background workers, daemons, multi-tenant                   | Standard DI (`IServiceCollection`) | `IMetricTracker`, `[FromKeyedServices]`, `IMetricFlow` | [AdvancedConsoleWithDIExample](examples/AdvancedConsoleWithDIExample)                                           |
| **Console Log Sink & Triggers**   | Real-time formatted console logging, timer-free sampling    | Standalone or DI                   | `AddConsoleSink(...)`, `ConfigureSinkSampling(...)`    | [ConsoleSinkExample](examples/ConsoleSinkExample)                                                               |
| **Hierarchical Scope Trees**      | Parent-child execution flows, self-time / self-memory       | Standalone or DI                   | `AddHierarchyCounter()`, `GetHierarchySnapshot(...)`   | [ConsoleSinkExample](examples/ConsoleSinkExample)                                                               |
| **Logger Sink (Serilog / ILogger)** | Centralized structured logging, APM forwarders, log files   | Standalone or DI                   | `AddLoggerSink(...)`, `ConfigureSinkSampling(...)`     | [LoggerSinkExample](examples/LoggerSinkExample)                                                                 |
| **Web Implementation**            | Web APIs, microservices, HTTP routing                       | ASP.NET Core pipeline              | `app.UseMetricFlow()`, `app.MapMetricFlow("/metrics")` | [WebApiExample](examples/WebApiExample)                                                                         |
| **OpenTelemetry & Observability** | Prometheus, Grafana, Datadog, OTLP collectors, CLI counters | OpenTelemetry SDK / BCL            | `.AddMetricFlowInstrumentation()`, `dotnet-counters`   | [OpenTelemetryConsoleExample](examples/OpenTelemetryConsoleExample)                                             |

---

## Features

- **Counters**: Track execution counts and occurrences of events.
- **Timers & Duration**: High-precision operation timing via lock-free stopwatch ticks.
- **Throughput & Item Tracking**: Measure batch sizes, entity counts, and processing rates (items/sec) with `ThroughputCounter`.
- **Asynchronous Stream Tracking**: Native `TrackStream` for `IAsyncEnumerable<T>` and `IEnumerable<T>` with automatic item counting and duration tracking.
- **Hierarchical Metrics & Correlation**: Automatically correlate parent and nested child tracking scopes across asynchronous execution contexts (`AsyncLocal`) with `HierarchyCounter`. Computes exclusive self-duration and self-allocated memory, renders formatted tree snapshots (`▼`, `├─`, `└─`), and bridges to ambient OpenTelemetry Activity spans.
- **Dimensional Breakdown & Slicing**: Slice and compute operation distributions by business dimensions, tags, or computed rules with `DimensionCounter` and built-in cardinality safeguards.
- **Memory Tracking**: Measure per-operation heap allocations with `MemoryCounter`.
- **Exception & Failure Tracking**: Capture runtime exceptions with `ExceptionCounter` and track logical versus exception failure distributions with `FailureCounter`.
- **Structured Sinks & Dynamic Color Coding**: Real-time console and logger sinks with threshold color rules (`AddThreshold`), dynamic pattern selectors (`ColorSelector`), customizable units (`DurationUnit`, `MemoryUnit`, `ThroughputUnit`), and extensible snapshot formatters (`AddFormatter`).
- **OpenTelemetry Integration**: Turnkey integration via [`DotnetKit.MetricFlow.OpenTelemetry`](src/DotnetKit.MetricFlow.OpenTelemetry/README.md) for exporting metrics to Prometheus, Grafana, Datadog, and OTLP collectors with ambient distributed trace correlation. See the [OpenTelemetry README](src/DotnetKit.MetricFlow.OpenTelemetry/README.md).
- **Built on .NET Diagnostics**: Built directly on native .NET BCL `System.Diagnostics.Metrics` (`Meter`, `Histogram`, `Counter`, `UpDownCounter`) with lock-free hot paths and cardinality protection. See the [Architecture Documentation](ARCHITECTURE.md#11-under-the-hood-systemdiagnostics-bridge).
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
var throughput = tracker.GetThroughputSnapshot("ImportChannels");
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
var countryDim = tracker.GetDimensionSnapshot("ProcessOrder", "country");
var paymentDim = tracker.GetDimensionSnapshot("ProcessOrder", "PaymentChannels");
var tierDim = tracker.GetDimensionSnapshot("ProcessOrder", "CustomerTier");
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

##### Stream Tracking (`TrackStream` for `IAsyncEnumerable<T>` / `IEnumerable<T>`)

Seamlessly track asynchronous or synchronous data streams with automatic duration measurement, yielded items counting for throughput calculation, and exception capture:

```csharp
// In an adapter, repository, or use-case:
return _programmesClient.FindManyAsync(...)
    .Select(item => item.ToProgram())
    .TrackStream(_tracker, "TableStorage.GetProgrammesForChannel");

// With cancellation support and CallerMemberName:
public async IAsyncEnumerable<Item> GetItemsAsync([EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var item in source.TrackStream(_tracker, ct))
    {
        yield return item;
    }
}
```

##### Hierarchical Scope Tracking (`HierarchyCounter` / `GetHierarchySnapshot`)

Correlate parent and nested child operations across asynchronous execution contexts (`AsyncLocal`) with zero manual token passing. MetricFlow computes exclusive self-duration and self-allocated memory by subtracting child metrics from total parent values:

```csharp
// 1. Enable hierarchy counter on tracker or DI options
tracker.AddHierarchyCounter();

// 2. Track parent and nested child operations naturally
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
    }
}

// 3. Inspect formatted execution tree
var tree = tracker.GetHierarchySnapshot("GetChannelProgramsUseCase");
Console.WriteLine(tree?.ToFormattedString());
/* Output:
▼ [GetChannelProgramsUseCase] (75.82 ms, self: 0.82 ms | alloc: 24.50 KB, self: 9.10 KB)
  ├─ [MatchSingleChannelUseCase] (10.20 ms | items: 1 | alloc: 4.25 KB)
  └─ [TableStorage.GetProgrammesForChannel] (64.80 ms | items: 1450 | alloc: 11.15 KB)
*/
```

##### Querying Typed Snapshots (`GetSnapshot<T>` & Value Helpers)

MetricFlow allows you to retrieve strongly-typed telemetry snapshots programmatically using either generic queries or dedicated helper methods:

```csharp
// 1. Generic snapshot queries by snapshot type
DurationSnapshot? duration   = tracker.GetSnapshot<DurationSnapshot>("ProcessOrder");
ThroughputSnapshot? items     = tracker.GetSnapshot<ThroughputSnapshot>("ProcessOrder");
ExceptionSnapshot? errors    = tracker.GetSnapshot<ExceptionSnapshot>("ProcessOrder");
FailureSnapshot? failures    = tracker.GetSnapshot<FailureSnapshot>("ProcessOrder");
MemorySnapshot? memory       = tracker.GetSnapshot<MemorySnapshot>("ProcessOrder");

// With an explicit counter name (e.g. for custom counters or specific dimensions)
DimensionSnapshot? regionDim = tracker.GetSnapshot<DimensionSnapshot>("ProcessOrder", "Dimension:region");

// 2. Query multiple snapshots of the same type (e.g. all dimension breakdowns for an operation)
IEnumerable<DimensionSnapshot> allDims = tracker.GetSnapshots<DimensionSnapshot>("ProcessOrder");

// 3. Query all snapshots of a given type across the entire tracker
IEnumerable<ExceptionSnapshot> allErrors = tracker.GetAllSnapshots<ExceptionSnapshot>();

// 4. Dedicated typed helper methods (internally powered by GetSnapshot<T>)
DurationSnapshot? duration2   = tracker.GetDurationSnapshot("ProcessOrder");
ThroughputSnapshot? items2    = tracker.GetThroughputSnapshot("ProcessOrder");
ExceptionSnapshot? errors2   = tracker.GetExceptionSnapshot("ProcessOrder");
FailureSnapshot? failures2   = tracker.GetFailureSnapshot("ProcessOrder");
MemorySnapshot? memory2      = tracker.GetMemorySnapshot("ProcessOrder");
DimensionSnapshot? dimension = tracker.GetDimensionSnapshot("ProcessOrder", "region");
```

#### 4. Dependency Injection (Console Apps, Workers & Daemons)

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

#### 5. ASP.NET Core Integration

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

#### 6. OpenTelemetry & Cloud Telemetry

MetricFlow seamlessly bridges domain metrics and scoped tracking to the standard OpenTelemetry .NET ecosystem via [`DotnetKit.MetricFlow.OpenTelemetry`](src/DotnetKit.MetricFlow.OpenTelemetry/README.md).

For full setup guides, fluent builder APIs (`.WithOpenTelemetry()`), standalone meter provider instrumentation (`.AddMetricFlowInstrumentation()`), BCL instrument mappings, and ambient trace correlation, see the **[DotnetKit.MetricFlow.OpenTelemetry README](src/DotnetKit.MetricFlow.OpenTelemetry/README.md)**.

#### 7. Live Terminal Diagnostics (`dotnet-counters`)

Because MetricFlow is backed directly by `System.Diagnostics.Metrics.Meter`, you can inspect active metrics in real time in your terminal without configuring any external collector:

```sh
# Monitor live operations for topic "OrderProcessingService"
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow.OrderProcessingService

# Or monitor across all MetricFlow topics
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow
```

---

## Architecture

MetricFlow is engineered around lock-free hot-path execution, decoupled state-token lifecycles, and a zero-dependency core bridging directly to .NET BCL `System.Diagnostics.Metrics`.

For detailed architecture, hot-path dispatch mechanics, the `System.Diagnostics` bridge, concurrency model, and custom counter lifecycles, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Examples

- **[BasicConsoleExample](examples/BasicConsoleExample)**: Simplest implementation demonstrating minimal tracker setup and duration measurement with zero optional counters.
- **[AdvancedConsoleExample](examples/AdvancedConsoleExample)**: Full multi-counter demonstration including duration, throughput (items/sec and batch sizing), memory allocation, exceptions, and delegate tracking.
- **[AdvancedConsoleWithDIExample](examples/AdvancedConsoleWithDIExample)**: Standard Microsoft DI integration demonstrating fluent builder (`AddMetricTracker`), `AddTagsEnricher`, multi-topic tracking, keyed services (`[FromKeyedServices]`), worker pipelines, and programmatic telemetry queries.
- **[ConsoleSinkExample](examples/ConsoleSinkExample)**: Structured console log sink (`ConsoleMetricSink`) featuring ANSI color coding, threshold rules (`AddThreshold`), dynamic pattern selectors, hierarchical parent-child execution trees, configurable units (`DurationUnit`, `MemoryUnit`, `ThroughputUnit`), and timer-free execution-lifecycle sampling triggers.
- **[LoggerSinkExample](examples/LoggerSinkExample)**: Structured `ILogger` sink (`LoggerMetricSink`) integrated with Serilog, demonstrating dynamic log level elevation on failures, structured property extraction, and timer-free sampling triggers.
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

# Structured console log sink example (ConsoleMetricSink, timer-free lifecycle triggers)
dotnet run --project examples/ConsoleSinkExample

# Structured ILogger sink example (Serilog integration, dynamic log levels)
dotnet run --project examples/LoggerSinkExample

# OpenTelemetry console example (OpenTelemetry SDK, BCL bridge, console exporter)
dotnet run --project examples/OpenTelemetryConsoleExample

# ASP.NET Core Web API example
dotnet run --project examples/WebApiExample
```

### Extensibility

MetricFlow is designed to be extensible. You can implement custom counters by deriving from `CounterBase<TState>` and pre-configure trackers by inheriting from `MetricTrackerBase`.

See [`examples/CustomCounters`](examples/CustomCounters) and the [Architecture Guide](ARCHITECTURE.md#8-creating-a-custom-counter) for complete implementation patterns.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the development roadmap, upcoming milestones, and architectural improvements.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes, releases, and fixes.
