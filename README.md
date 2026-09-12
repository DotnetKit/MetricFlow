# MetricFlow

![internal](https://github.com/dotnetkit/metricflow/actions/workflows/publish-internal.yml/badge.svg)
![public](https://github.com/dotnetkit/metricflow/actions/workflows/publish-public.yml/badge.svg)
![Dotnetkit.MetricFlow](https://img.shields.io/nuget/v/Dotnetkit.MetricFlow)

MetricFlow is a lightweight .NET library designed to help developers define and track functional and domain-oriented metrics (such as counters, timers, and event-based measurements).

## Features

- **Counters**: Track the number of occurrences of an event.
- **Timers**: Measure the duration of operations.
- **Event-based Measurements**: Capture and analyze specific events within your application.
- **Metadata and Tags**: Add contextual information to your metrics for better analysis and filtering.
- **Sampling**: Control the frequency of metric collection to manage performance and data volume.

## Getting Started

### Prerequisites

- .NET SDK installed on your machine

### Installation

1. Clone the repository:

    ```sh
    git clone https://github.com/yourusername/DotnetKit.git
    cd DotnetKit/MetricFlow
    ```

2. Restore dependencies:

    ```sh
    dotnet restore
    ```

### Usage

#### 1. Initialize Tracker

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("OrderService", new()
{
    ["environment"] = "production"
});
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

### Examples

- **[SimpleMetricCountersExample](examples/SimpleMetricCountersExample)**: Demonstrates scope tracking, `TrackActionAsync`, custom tags, memory, and exception counters.
- **[WebApiExample](examples/WebApiExample)**: Demonstrates ASP.NET Core integration and metrics endpoints.

Run the simple example:

```sh
dotnet run --project examples/SimpleMetricCountersExample
```

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the development roadmap, upcoming milestones, and architectural improvements.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes, releases, and fixes.
