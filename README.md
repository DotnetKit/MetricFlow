# MetricFlow

![internal](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-internal.yml/badge.svg)
![public](https://github.com/DotnetKit/MetricFlow/actions/workflows/publish-public.yml/badge.svg)
[![DotnetKit.MetricFlow](https://img.shields.io/nuget/v/DotnetKit.MetricFlow)](https://www.nuget.org/packages/DotnetKit.MetricFlow)
[![DotnetKit.MetricFlow.AspNetCore](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.AspNetCore)](https://www.nuget.org/packages/DotnetKit.MetricFlow.AspNetCore)

MetricFlow is a lightweight .NET library designed to help developers define and track functional and domain-oriented metrics (such as counters, timers, and event-based measurements).

## Features

- **Counters**: Track execution counts and occurrences of events.
- **Timers & Duration**: High-precision operation timing via lock-free stopwatch ticks.
- **Throughput & Item Tracking**: Measure batch sizes, entity counts, and processing rates (items/sec) with `ThroughputCounter`.
- **Tag & Dimensional Breakdown**: Slice and compute operation distributions by business tags with `TagBreakdownCounter` and built-in cardinality safeguards.
- **Memory Tracking**: Measure per-operation heap allocations with `MemoryCounter`.
- **Exception & Failure Tracking**: Capture errors, exceptions, and failure counts with `ExceptionCounter`.
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

Initialize a tracker and optionally chain throughput, memory, exception, and tag breakdown counters:

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("OrderService", new()
    {
        ["environment"] = "production"
    })
    .AddThroughputCounter()
    .AddMemoryCounter()
    .AddExceptionCounter()
    .AddTagBreakdownCounter("country");
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

##### Tag & Dimensional Breakdown (`AddTagBreakdownCounter` / `scope.SetTag`)

Slice and categorize operation counts by business tags (e.g. `country`, `tenant_id`, `status`) with built-in cardinality safeguards:

```csharp
// 1. Register counter for a specific tag key with optional cardinality limit (defaults to 250)
tracker.AddTagBreakdownCounter("country", maxUniqueValues: 100);

// 2. Track with tag dictionary
using (tracker.Track("ProcessOrder", new() { ["country"] = "US" }))
{
    // ...
}

// 3. Or set tag dynamically on the active scope
using (var scope = tracker.Track("ProcessOrder"))
{
    var country = ResolveCustomerCountry();
    scope.SetTag("country", country);
}

// 4. Inspect snapshot breakdown
var breakdown = tracker.GetTagBreakdownValues("ProcessOrder", "country");
// breakdown.TotalOperations  -> 100
// breakdown.TaggedOperations -> 95 (95.0%)
// breakdown.Breakdown["US"]  -> 60
// breakdown.Breakdown["DE"]  -> 35
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
    options.TopicTags = new() { ["env"] = "Production" };
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

### Examples

- **[BasicConsoleExample](examples/BasicConsoleExample)**: Simplest implementation demonstrating minimal tracker setup and duration measurement with zero optional counters.
- **[AdvancedConsoleExample](examples/AdvancedConsoleExample)**: Full multi-counter demonstration including duration, throughput (items/sec and batch sizing), memory allocation, exceptions, and delegate tracking.
- **[WebApiExample](examples/WebApiExample)**: Demonstrates ASP.NET Core integration, middleware, and `/metrics` endpoint.
- **[CustomCounters](examples/CustomCounters)**: Demonstrates extension capabilities by implementing custom counters and trackers.

Run the examples:

```sh
# Basic console example (minimal setup)
dotnet run --project examples/BasicConsoleExample

# Multi-counter console example (advanced: duration, throughput, memory, exceptions)
dotnet run --project examples/AdvancedConsoleExample

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
