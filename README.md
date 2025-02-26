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

To use MetricFlow in your project, follow these steps:

1. Define a metric tracker:

    ```csharp
    var tracker = new MetricTracker("ExecutionTimeMetricsTopic", new()
    {
        ["tenant_id"] = "TenantId1",
        ["session_id"] = Guid.NewGuid().ToString()
    });
    ```

2. Track metrics in different ways:

    ```csharp
    tracker.In("GlobalOperation");

    for (var i = 0; i < 10; i++)
    {
        using (var __ = tracker.Track("Operation1", new() { ["operation_id"] = $"{i}" }))
        {
            await Task.Delay(2);
        }
        using (var __ = tracker.Track("Operation2", new() { ["operation_id"] = $"{10 - i}" }))
        {
            await Task.Delay(4);
        }
    }

    tracker.Out("GlobalOperation");

    Console.WriteLine(tracker.ToString());
    ```

### Example

The `SimpleMetricCountersExample` demonstrates how to use the `MetricFlow` library to track and measure metrics in a .NET application. The example includes a `BenchRunner` class that simulates operations and tracks their execution times.

### Running the Example

To run the example, execute the following command:

```sh
dotnet run --project examples/SimpleMetricCountersExample
