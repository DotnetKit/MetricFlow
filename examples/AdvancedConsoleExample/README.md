# Simple Metric Counters Example

This example demonstrates how to use the `MetricFlow` library to track and measure metrics in a .NET application. The example includes a `BenchRunner` class that simulates operations and tracks their execution times.

## Getting Started

### Prerequisites

- .NET SDK installed on your machine

### Installation

1. Clone the repository:

    ```sh
    git clone https://github.com/yourusername/DotnetKit.git
    cd DotnetKit/MetricFlow/examples/SimpleMetricCountersExample
    ```

2. Restore dependencies:

    ```sh
    dotnet restore
    ```

### Running the Example

To run the example, execute the following command:

```sh
dotnet run
```

## Example Overview

The `BenchRunner` class simulates a series of operations and tracks their execution times using the `MetricTracker` class.

### Key Components

- **MetricTracker**: Tracks metrics for different operations.
  - two way usage: code scoped tracker (disposable pattern) or control tracker directly (with In() and Out() calls)
- **BenchRunner**: Simulates operations and uses `MetricTracker` to measure their execution times.

### Code Example

```csharp
public static class BenchRunner
{
    private const int OPERATION_COUNT = 10;

    public static async Task RunExample()
    {
        var tracker = new MetricTracker("ExecutionTimeMetricsTopic", new()
        {
            ["tenant_id"] = "TenantId1",
            ["session_id"] = Guid.NewGuid().ToString()
        });

        tracker.In("GlobalOperation");

        for (var i = 0; i < OPERATION_COUNT; i++)
        {
            using (var __ = tracker.Track("Operation1", new() { ["operation_id"] = $"{i}" }))
            {
                await Task.Delay(2);
            }
            using (var __ = tracker.Track("Operation2", new() { ["operation_id"] = $"{OPERATION_COUNT - i}" }))
            {
                await Task.Delay(4);
            }
        }

        tracker.Out("GlobalOperation");

        Console.WriteLine(tracker.ToString());
    }
}
```

### Output

The output will display the tracked metrics, including the count, average duration, minimum and maximum durations, and total duration for each operation.

```
ExecutionTimeMetricsTopic
Topic Tags:  tenant_id:TenantId1, session_id:74f627d9-5787-42b1-bab6-f1953ac3e215
GlobalOperation
MetricMetadata:
Count (in, out): 1 / 1
Avg duration: 1336323 ms
Duration (min, max) : 1336323 ms / 1336323 ms
Total duration: 1336323 ms

Operation1
MetricMetadata:  operation_id:0
Count (in, out): 10 / 10
Avg duration: 29906 ms
Duration (min, max) : 22745 ms / 182873 ms
Total duration: 615900 ms

Operation2
MetricMetadata:  operation_id:10
Count (in, out): 10 / 10
Avg duration: 112361 ms
Duration (min, max) : 41157 ms / 166233 ms
Total duration: 695333 ms
```

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or bug fixes.

## License

This project is licensed under the MIT License.
