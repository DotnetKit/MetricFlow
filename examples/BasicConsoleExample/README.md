# Basic Console Example

This example demonstrates the minimal, zero-dependency setup for **MetricFlow** in a .NET console application. It showcases basic operation tracking using scoped disposable blocks (`tracker.Track`) and synchronous delegate tracking (`tracker.TrackAction`).

---

## Highlights

- **Zero Configuration Required**: Uses the default `DurationCounter` out of the box.
- **Topic & Static Tags**: Attaches topic-level tags (`environment: Development`) that propagate to all tracked operations.
- **Scoped Disposable Tracking (`tracker.Track`)**: Measures operation latency and lifecycle using standard C# `using` scopes.
- **Delegate Tracking (`tracker.TrackAction`)**: Wraps synchronous actions with automatic latency measurement and exception handling.
- **Formatted Telemetry Output**: Formats human-readable aggregated metrics via `tracker.ToString()`.

---

## Code Overview

```csharp
using DotnetKit.MetricFlow;

// 1. Initialize tracker with topic name and topic-level tags
var tracker = new MetricTracker("BasicConsoleTopic", new()
{
    ["environment"] = "Development"
});

Console.WriteLine("Executing operations with MetricTracker...\n");

// 2. Scoped tracking with using statement
for (var i = 1; i <= 5; i++)
{
    using (tracker.Track("ProcessOrder", new() { ["order_id"] = $"{i}" }))
    {
        await Task.Delay(10);
    }
}

// 3. Delegate tracking with TrackAction
for (var i = 1; i <= 3; i++)
{
    tracker.TrackAction("ValidatePayment", () => Thread.Sleep(5));
}

// 4. Print formatted telemetry snapshots
Console.WriteLine(tracker.ToString());
```

---

## Running the Example

From the repository root:

```bash
dotnet run --project examples/BasicConsoleExample/BasicConsoleExample.csproj
```

### Expected Output

```text
BasicConsoleTopic
Topic Tags:
  - environment: Development

ProcessOrder
Duration (ms):
  Avg: 11.23  Min: 10.45  Max: 12.10  Total: 56.15
  Operations: 5 in / 5 out (0 failed)

ValidatePayment
Duration (ms):
  Avg: 5.12  Min: 5.01  Max: 5.34  Total: 15.36
  Operations: 3 in / 3 out (0 failed)
```
