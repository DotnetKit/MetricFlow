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

Initialize a tracker and optionally chain throughput, memory, and exception counters:

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("OrderService", new()
    {
        ["environment"] = "production"
    })
    .AddThroughputCounter()
    .AddMemoryCounter()
    .AddExceptionCounter();
```

#### 2. Tracker Capabilities

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

- **[SimpleMetricCountersExample](examples/SimpleMetricCountersExample)**: Demonstrates scope tracking, `TrackActionAsync`, custom tags, memory, and exception counters.
- **[WebApiExample](examples/WebApiExample)**: Demonstrates ASP.NET Core integration, middleware, and `/metrics` endpoint.
- **[CustomCounters](examples/CustomCounters)**: Demonstrates extension capabilities by implementing custom counters and trackers.

Run the examples:

```sh
# Simple console example
dotnet run --project examples/SimpleMetricCountersExample

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
        _inner.OnOut(state, new OutContext(context.MetricName, context.Failed, context.Exception, elapsed, context.Tags));
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
