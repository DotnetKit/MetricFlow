# Hierarchical Scope Trees

The **Hierarchical Scope Trees** pattern automatically correlates parent and child tracking scopes across asynchronous execution flows (`AsyncLocal`). Powered by [`HierarchyCounter`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Counters/HierarchyCounter.cs), it calculates **exclusive self-duration** and **self-allocated memory** for each node, renders visual execution trees (`▼`, `├─`, `└─`), and automatically bridges nested spans into ambient OpenTelemetry `Activity` distributed traces.

---

## Best For

- **Complex Execution Pipelines**: Multi-step workflows (e.g. Ingestion -> Validation -> Storage -> Notification).
- **Bottleneck Diagnosis**: Distinguish between slow child dependencies (database, external HTTP) and CPU-bound overhead in the parent method.
- **Memory Allocation Auditing**: Identify which exact layer in a call stack causes heap allocations.
- **Distributed Trace Bridging**: Seamlessly map nested in-memory code blocks into OpenTelemetry spans without manually creating `Activity` instances.

---

## Quick Start

Enable the hierarchy counter, execute nested operations, and view the formatted execution tree:

```csharp
using DotnetKit.MetricFlow;

var tracker = new MetricTracker("CatalogService")
    .AddHierarchyCounter()
    .AddMemoryCounter()
    .AddThroughputCounter()
    .AddConsoleSink(opt =>
    {
        opt.Colorize = true;
        opt.ShowHierarchicalTree = true;
    });

// Parent scope
using (tracker.Track("GetChannelProgramsUseCase"))
{
    await Task.Delay(20);

    // Child scope 1: Quick cache lookup
    using (var step1 = tracker.Track("MatchSingleChannelUseCase"))
    {
        step1.SetItems(1);
        await Task.Delay(10);
    }

    // Child scope 2: Database fetch
    using (var step2 = tracker.Track("TableStorage.GetProgrammesForChannel"))
    {
        step2.SetItems(1450);
        await Task.Delay(50);

        // Grandchild scopes
        using (tracker.Track("TableStorage.QuerySegmentAsync"))
        {
            await Task.Delay(20);
        }
    }
}

// Flush and display the tree snapshot
await tracker.FlushSinksAsync();
```

### Formatted Console Output

```text
[CatalogService] Hierarchy Execution Tree:
▼ GetChannelProgramsUseCase (total: 80.45 ms, self: 20.45 ms, alloc: 12.4 KB)
  ├─ MatchSingleChannelUseCase (total: 10.12 ms, self: 10.12 ms, items: 1)
  └─ TableStorage.GetProgrammesForChannel (total: 50.15 ms, self: 30.15 ms, items: 1,450)
      └─ TableStorage.QuerySegmentAsync (total: 20.00 ms, self: 20.00 ms)
```

---

## Available Options & API Reference

### 1. Counter Registration

| Method | Description |
| :--- | :--- |
| `AddHierarchyCounter(string name = "Hierarchy")` | Registers a [`HierarchyCounter`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Counters/HierarchyCounter.cs) with an optional custom counter name. |
| `EnableHierarchical(bool enabled = true)` | Fluent helper alias on [`MetricFlowOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/MetricFlowOptions.cs) to ensure the hierarchy counter is active. |

### 2. Node Properties in `HierarchyNode`

When inspecting nodes via [`HierarchyNode`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Hierarchy/HierarchyNode.cs):

| Property | Type | Description |
| :--- | :--- | :--- |
| `MetricName` | `string` | The operation name assigned when tracked. |
| `Parent` | `HierarchyNode?` | Reference to parent scope node (or null if root). |
| `Children` | `IReadOnlyList<HierarchyNode>` | Child operations executed within this scope. |
| `Duration` | `TimeSpan` | Total elapsed duration including all child executions. |
| `SelfDuration` | `TimeSpan` | Exclusive duration spent solely inside this node (`Duration - Sum(Children.Duration)`). |
| `AllocatedBytes` | `long` | Total heap memory allocated on current thread during scope. |
| `SelfAllocatedBytes` | `long` | Exclusive heap memory allocated by this node alone (`AllocatedBytes - Sum(Children.AllocatedBytes)`). |
| `ItemCount` | `long` | Number of items processed by this operation. |
| `Failed` | `bool` | Whether the scope failed or encountered an exception. |
| `Exception` | `Exception?` | Exception captured during scope execution, if any. |

### 3. Tree Snapshot & Retrieval

```csharp
// Retrieve hierarchy snapshot for a root operation
var snapshot = tracker.GetHierarchySnapshot("GetChannelProgramsUseCase");

if (snapshot != null)
{
    HierarchyNode root = snapshot.RootNode;
    Console.WriteLine($"Root: {root.MetricName}, Total: {root.Duration}, Self: {root.SelfDuration}");
}
```

---

## Frequently Asked Questions (FAQ)

### Q: How does MetricFlow calculate self-duration versus total duration?

**A:** Total duration measures the wall-clock time between `In` and `Out` of the scope. Self-duration subtracts the cumulative duration of all immediate child scopes:

$$\text{SelfDuration} = \max(0, \text{TotalDuration} - \sum \text{Child.Duration})$$

This isolates the exact CPU processing or waiting time in the current function without skewing metrics from downstream calls.

---

### Q: Does hierarchy tracking work safely across asynchronous methods and `await` calls?

**A:** Yes. `HierarchyCounter` utilizes .NET `AsyncLocal<HierarchyNode?>`. The tracking context automatically flows down through `async`/`await` state machine boundaries, child tasks, and thread pool hops, restoring the parent scope when child operations complete.

---

### Q: How do hierarchical scopes bridge into OpenTelemetry Activity spans?

**A:** `HierarchyCounter` exposes a native `System.Diagnostics.ActivitySource` (`"DotnetKit.MetricFlow"`). When an OpenTelemetry tracer is active:
1. Entering a scope starts a child `Activity` parented to the ambient trace context.
2. Scope tags are mapped to span attributes.
3. Upon exit, `metricflow.self_duration_ms`, `metricflow.allocated_bytes`, and `metricflow.items` are attached to the span.
4. If an exception occurs, the span status is set to `ActivityStatusCode.Error`.

---

### Q: How can I programmatically traverse all nodes in a hierarchy tree?

**A:** You can write a recursive visitor using the `Children` collection:

```csharp
var snapshot = tracker.GetHierarchySnapshot("GetChannelProgramsUseCase");
if (snapshot != null)
{
    PrintTree(snapshot.RootNode, depth: 0);
}

void PrintTree(HierarchyNode node, int depth)
{
    string indent = new(' ', depth * 2);
    Console.WriteLine($"{indent}- {node.MetricName} (Self: {node.SelfDuration.TotalMilliseconds:N1}ms)");
    foreach (var child in node.Children)
    {
        PrintTree(child, depth + 1);
    }
}
```

---

### Q: What happens if an exception is thrown in a nested grandchild scope?

**A:** When an exception unwinds the stack:
- The grandchild scope's `Dispose()` marks the node as `Failed = true` and records the `Exception`.
- The parent scope still captures its elapsed time and registers its execution snapshot when it completes.
- In console sinks, failed nodes are visually highlighted in Red with the exception message.
