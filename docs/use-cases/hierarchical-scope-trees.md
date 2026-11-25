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
        opt.DisplayMode = ConsoleDisplayMode.Table;
    });

// Parent scope (Track or In/Out)
using (tracker.Track("GetChannelProgramsUseCase"))
{
    await Task.Delay(20);

    // Child scope 1: automatically correlated under GetChannelProgramsUseCase
    using (var step1 = tracker.Track("MatchSingleChannelUseCase"))
    {
        step1.SetItems(1);
        await Task.Delay(10);
    }

    // Child scope 2: automatically correlated under GetChannelProgramsUseCase
    using (var step2 = tracker.Track("TableStorage.GetProgrammesForChannel"))
    {
        step2.SetItems(1450);
        await Task.Delay(50);

        // Grandchild scope: automatically correlated under TableStorage.GetProgrammesForChannel
        using (tracker.Track("TableStorage.QuerySegmentAsync"))
        {
            await Task.Delay(20);
        }
    }
}

Console.WriteLine(tracker.ToMarkdownTable());
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
var parent = tracker.GetParentOperation("MatchSingleChannelUseCase");
```
