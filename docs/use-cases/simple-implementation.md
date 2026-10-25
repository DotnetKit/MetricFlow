# Simple Implementation (Standalone Tracker)

The **Simple Implementation** pattern uses a standalone, lightweight [`MetricTracker`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/MetricTracker.cs) instance directly in your application without requiring dependency injection, an inversion-of-control container, or external configuration files.

---

## Best For

- **Targeted Method & Loop Profiling**: Measure specific bottlenecks, batch jobs, background tasks, or micro-benchmarks.
- **Console & CLI Applications**: Lightweight utilities, data migration scripts, and ad-hoc command-line tools.
- **Library Code & SDKs**: Self-contained component tracking without forcing DI dependencies on consumers.
- **Algorithm & Data Structure Benchmarking**: Low-overhead measurements with atomic CPU primitives and lock-free hot paths.

---

## Quick Start

By default, [`MetricTracker`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/MetricTracker.cs) records high-precision execution duration via lock-free stopwatch ticks:

```csharp
using DotnetKit.MetricFlow;

// 1. Initialize tracker with topic name and topic-level tags
var tracker = new MetricTracker("OrderService", new()
{
    ["environment"] = "Production",
    ["region"] = "us-east-1"
});

// 2. Track execution with a using scope
using (tracker.Track("ProcessOrder", new() { ["order_id"] = "1001" }))
{
    await ExecuteOrderAsync();
}

// 3. Track delegates directly
tracker.TrackAction("ValidatePayment", () => Thread.Sleep(5));

// 4. Print formatted telemetry summary
Console.WriteLine(tracker.ToString());
```

---

## Available Options & API Reference

### 1. Tracker Constructors

| Constructor | Description |
| :--- | :--- |
| `new MetricTracker(string topic = "Application")` | Initializes a tracker with a topic name and default duration tracking. |
| `new MetricTracker(string topic, Dictionary<string, string>? topicTags)` | Initializes a tracker with topic name and contextual topic-level tags. |
| `new MetricTracker(MetricFlowOptions options)` | Initializes a tracker using a pre-configured [`MetricFlowOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/MetricFlowOptions.cs) object. |

### 2. Primary Tracking Methods

| Method | Signature | Description |
| :--- | :--- | :--- |
| `Track` | `CodeTracker Track(string metricName, Dictionary<string, string>? tags = null)` | Creates a disposable tracking scope. Captures duration upon disposal. |
| `TrackItems` | `CodeTracker TrackItems(string metricName, long itemCount, Dictionary<string, string>? tags = null)` | Starts a tracking scope pre-seeded with processed item count for throughput calculations. |
| `TrackAction` | `void TrackAction(string metricName, Action action, Dictionary<string, string>? tags = null)` | Executes a synchronous delegate within a managed tracking scope. |
| `TrackAction<T>` | `T TrackAction<T>(string metricName, Func<T> action, Dictionary<string, string>? tags = null)` | Executes a synchronous function, returning its result while tracking metrics. |
| `TrackActionAsync` | `Task TrackActionAsync(string metricName, Func<Task> action, Dictionary<string, string>? tags = null)` | Executes an asynchronous delegate within a tracking scope. |
| `TrackActionAsync<T>` | `Task<T> TrackActionAsync<T>(string metricName, Func<Task<T>> action, Dictionary<string, string>? tags = null)` | Executes an asynchronous function returning a result. |
| `TrackStream` | `IAsyncEnumerable<T> TrackStream<T>(string metricName, IAsyncEnumerable<T> source, ...)` | Instruments an `IAsyncEnumerable<T>` stream, capturing item counts and stream duration. |
| `In` / `Out` | `void In(string metricName, ...)` / `void Out(string metricName, ...)` | Explicit entry and exit lifecycle hooks for non-scoped or multi-stage workflows. |

### 3. Registering Built-in Counters

Chaining fluent counter registration methods extends telemetry collection:

```csharp
var tracker = new MetricTracker("DataIngestion")
    .AddThroughputCounter()                      // Measures items/sec and batch volumes
    .AddMemoryCounter()                          // Captures heap allocation deltas
    .AddExceptionCounter()                       // Records thrown exception distributions
    .AddFailureCounter()                         // Tracks logical vs exception failures
    .AddHierarchyCounter()                       // Correlates parent-child execution scopes
    .AddDimensionCounter("customer_tier")        // Groups metrics by dimension tags
    .AddDimensionCounter("tenant_region", ["tenant", "region"]); // Multi-tag dimension
```

### 4. Direct Snapshot Queries & Export APIs

```csharp
// Programmatic snapshot retrieval
var duration = tracker.GetDurationSnapshot("ProcessOrder");
var throughput = tracker.GetThroughputSnapshot("BatchIngestion");
var dimension = tracker.GetDimensionSnapshot("DynamicProcessor", "customer_tier");
var hierarchy = tracker.GetHierarchySnapshot("ParentPipeline");

// Multi-format tabular exporters
string markdownTable = tracker.ToMarkdownTable(); // Formatted GitHub-flavored markdown table
string csvReport = tracker.ToCsv();               // RFC 4180 compliant CSV text
```

---

## Frequently Asked Questions (FAQ)

### Q: How do I measure operations without using `using` scopes?

**A:** Use the explicit `In` and `Out` methods. This is ideal for state machines, message queue consumers, or event-driven pipelines where start and finish events occur in separate methods:

```csharp
public class QueueConsumer
{
    private readonly MetricTracker _tracker = new("QueueProcessor");

    public void OnMessageReceived(string messageId)
    {
        _tracker.In("ProcessMessage", tags: new() { ["message_id"] = messageId });
    }

    public void OnMessageProcessed(string messageId, bool success, Exception? error = null)
    {
        _tracker.Out("ProcessMessage", failed: !success, exception: error);
    }
}
```

---

### Q: How can I dynamically set tags, metadata, or processed item counts inside an active scope?

**A:** Capture the returned `CodeTracker` scope. You can modify its state during execution before the scope disposes:

```csharp
using var scope = tracker.Track("DynamicBatch");

// Attach custom metadata
scope.SetMetadata("batch_size_bytes", 1024 * 64);

// Set or update item counts dynamically
long processedCount = await ProcessDynamicBatchAsync();
scope.SetItems(processedCount);

// Flag logical failures without throwing exceptions
if (processedCount == 0)
{
    scope.SetFailed(true);
}
```

---

### Q: How do I track asynchronous streams (`IAsyncEnumerable<T>`)?

**A:** Use the native `TrackStream` extension. It wraps your stream to track execution time and increments the item count as elements are yielded:

```csharp
IAsyncEnumerable<Order> orderStream = GetOrdersFromDatabaseAsync();

await foreach (var order in tracker.TrackStream("StreamOrders", orderStream))
{
    await ProcessOrderAsync(order);
}
```

---

### Q: How can I export metric summaries to CSV or Markdown files programmatically?

**A:** Use `ToCsv()` or `ToMarkdownTable()` directly on the tracker instance:

```csharp
// Export markdown table to documentation or GitHub Actions step summary
string markdown = tracker.ToMarkdownTable();
await File.WriteAllTextAsync("telemetry-summary.md", markdown);

// Export CSV for historical analysis or spreadsheet import
string csv = tracker.ToCsv();
await File.WriteAllTextAsync("telemetry-report.csv", csv);
```

---

### Q: How do I configure sampling to reduce overhead in ultra-high frequency loops?

**A:** Initialize the tracker using [`MetricFlowOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/MetricFlowOptions.cs) and set the `SamplingRate` property (between `0.0` and `1.0`):

```csharp
var options = new MetricFlowOptions
{
    Topic = "HighFrequencyPipeline",
    SamplingRate = 0.05 // Track 5% of operations probabilistically
};

var tracker = new MetricTracker(options);
```
