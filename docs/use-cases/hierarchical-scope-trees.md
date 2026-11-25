# Hierarchical Operation Correlation

MetricFlow automatically correlates parent and child operations across synchronous and asynchronous execution flows (`AsyncLocal<string?>`) using technical metadata (`MetricFlowMetadataKeys.Parent` = `"metricflow_parent"`), without requiring any dedicated counter or modifying business `tags`.

---

## Best For

- **Multi-Step Execution Pipelines**: Correlating parent workflows (e.g. `GlobalOperation` → `ExecuteOperation1Async`, `BatchIngestion`, `DynamicProcessor`).
- **Clean Business Tags**: Keeping `tags` strictly for business dimensions (`region`, `tenant_id`, `tier`) while storing structural/technical metadata (`metricflow_parent`, `trace_id`, `correlation_id`, `items`) in `metadata`.
- **Readable Reports**: Rendering indented parent-child rows (`└─ `) in `ToMarkdownTable()` and `ConsoleDisplayMode.Table`.

---

## Quick Start

Nested `Track`, `In`/`Out`, `TrackActionAsync`, and `TrackItems` calls are correlated automatically:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Exporters;
using DotnetKit.MetricFlow.Sinks.Console;

var tracker = new MetricTracker("CatalogService")
    .AddTelemetryCounters(includeMemory: true)
    .AddThroughputCounter()
    .AddConsoleSink(opt =>
    {
        opt.Colorize = true;
        opt.DisplayMode = ConsoleDisplayMode.Tree;
    });

// Parent scope (Track or In/Out)
using (tracker.Track("GetChannelProgramsUseCase"))
{
    await Task.Delay(30);

    // Child scope 1: automatically correlated under GetChannelProgramsUseCase
    using (var step1 = tracker.Track("Adapter.MatchSingleChannelUseCase"))
    {
        step1.SetItems(1);
        await Task.Delay(10);
    }

    // Child scope 2: automatically correlated under GetChannelProgramsUseCase
    using (var step2 = tracker.Track("Adapter.GetProgrammesForChannel"))
    {
        step2.SetItems(1450);
        await Task.Delay(65);

        // Grandchild scopes: automatically correlated under Adapter.GetProgrammesForChannel
        using (tracker.Track("TableStorage.QuerySegmentAsync"))
        {
            await Task.Delay(25);
        }

        using (tracker.Track("TableStorage.QuerySegmentAsync"))
        {
            await Task.Delay(25);
        }
    }
}

await tracker.FlushSinksAsync();
```

### Formatted Console Tree Output (`ConsoleDisplayMode.Tree`)

```text
[21:50:33.800] [Catalog:Trace]
▼ [GetChannelProgramsUseCase] (159.74 ms, self: 30.88 ms | items: 1 | alloc: 50.42 KB, self: 24.26 KB)
  ├─ [Adapter.MatchSingleChannelUseCase] (11.26 ms | items: 1 | alloc: 4.68 KB)
  └─ [Adapter.GetProgrammesForChannel] (117.60 ms, self: 66.24 ms | items: 1,450 | alloc: 21.48 KB, self: 15.60 KB)
      └─ [TableStorage.QuerySegmentAsync] x2 (51.35 ms, avg: 25.68 ms | items: 2 | alloc: 5.88 KB)
```

### Explicit Parent Override

When you want to declare or override a parent explicitly (for example, across disconnected scopes or message handlers), pass `metadata: new() { [MetricFlowMetadataKeys.Parent] = "ParentOperation" }` or call `.SetParent("ParentOperation")`:

```csharp
using (tracker.Track("StepA", metadata: new() { [MetricFlowMetadataKeys.Parent] = "WorkflowRoot", ["trace_id"] = "abc-123" }))
{
    // ...
}

using (var scope = tracker.Track("StepB").SetParent("WorkflowRoot"))
{
    // ...
}
```

---

## Programmatic Inspection

Each [`OperationSummary`](xref:DotnetKit.MetricFlow.Presentation.OperationSummary) exposes `ParentOperation` and `Depth`, ordered hierarchically by `tracker.GetOperationSummaries()`:

```csharp
var summaries = tracker.GetOperationSummaries();
foreach (var summary in summaries)
{
    Console.WriteLine($"{new string(' ', summary.Depth * 2)}{summary.OperationName} (parent: {summary.ParentOperation ?? "root"})");
}

// Or query a single operation's declared parent directly
var parent = tracker.GetParentOperation("Adapter.MatchSingleChannelUseCase");
```
