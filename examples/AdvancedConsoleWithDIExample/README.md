# Advanced Console Example with Dependency Injection (DI)

This example demonstrates how to integrate **MetricFlow** into a .NET console application, worker service, or background daemon using Microsoft Dependency Injection (`Microsoft.Extensions.DependencyInjection`).

---

## Highlights

- **Dependency Injection Without ASP.NET Core**: Configures MetricFlow in a clean, standalone DI container.
- **Fluent Topic Registration (`AddMetricFlow`)**: Registers primary and secondary metric topics in a fluent chain.
- **`AddTagsEnricher` Helper**: Fluent configuration of topic-level metadata tags (`tenant_id`, `session_id`, `environment`).
- **Multi-Counter Pipeline**: Chains `ThroughputCounter`, `ExceptionCounter`, `MemoryCounter`, and `DimensionCounter`.
- **Keyed Services (`[FromKeyedServices("AuditWorker")]`)**: Direct injection of named topic trackers into domain services.
- **Top-Level Facade (`IMetricFlow`)**: Unified enumeration, inspection, and snapshot reporting across all registered trackers.
- **Programmatic Snapshot Queries**: Directly retrieves typed metrics (`GetThroughputSnapshot`, `GetDimensionValues`) from trackers.

---

## Architecture & Code Walkthrough

### 1. DI Container Registration

```csharp
var services = new ServiceCollection();

// Configure root MetricFlow with topic, enriched tags, and multi-counter pipeline
services.AddMetricFlow("AdvancedConsoleDITopic", options =>
{
    // Use the AddTagsEnricher helper to configure topic-level tags
    options.AddTagsEnricher(tags =>
    {
        tags["tenant_id"] = "TenantId1";
        tags["session_id"] = Guid.NewGuid().ToString();
        tags["environment"] = "Production";
    })
    .AddThroughputCounter()
    .AddExceptionCounter()
    .AddMemoryCounter()
    .AddDimensionCounter("region");
})
// Fluently chain an isolated secondary topic tracker
.AddMetricTracker("AuditWorker", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["role"] = "security-auditor";
        tags["tier"] = "internal";
    });
});

// Register domain worker services
services.AddTransient<BatchProcessorService>();
services.AddTransient<AuditService>();
```

---

### 2. Service Injections

#### Primary Service (Default `IMetricTracker`)

Resolves the primary default tracker (`AdvancedConsoleDITopic`):

```csharp
public class BatchProcessorService(IMetricTracker tracker)
{
    public async Task RunAsync(int operationCount)
    {
        using var globalOp = tracker.Track("GlobalBatchRun");

        // 1. Scoped operation with memory allocation
        using (var scope = tracker.Track("SingleOperation", new() { ["operation_id"] = "1" }))
        {
            await Task.Delay(2);
        }

        // 2. Upfront throughput sizing
        using (var scope = tracker.TrackItems("BatchIngestion", 500))
        {
            await Task.Delay(4);
        }

        // 3. Dynamic throughput sizing with dimensional tagging
        using (var scope = tracker.Track("DynamicProcessor", new() { ["region"] = "EU" }))
        {
            await Task.Delay(2);
            scope.SetItems(300);
        }
    }
}
```

#### Keyed Service Injection (`[FromKeyedServices]`)

Resolves the secondary tracker (`AuditWorker`):

```csharp
public class AuditService([FromKeyedServices("AuditWorker")] IMetricTracker auditTracker)
{
    public async Task RunAuditAsync()
    {
        using var scope = auditTracker.Track("SecurityComplianceCheck");
        await Task.Delay(10);
    }
}
```

---

### 3. Unified Reporting via `IMetricFlow`

```csharp
var metricFlow = provider.GetRequiredService<IMetricFlow>();

// Enumerate and print snapshots across all registered trackers
foreach (var tracker in metricFlow.Trackers)
{
    Console.WriteLine(tracker.ToString());
}

// Access the default tracker directly for typed snapshot queries
var defaultTracker = metricFlow.DefaultTracker!;
var throughput = defaultTracker.GetThroughputSnapshot("BatchIngestion");
var dimension = defaultTracker.GetDimensionValues("DynamicProcessor", "region");
```

---

## Running the Example

From the repository root:

```bash
dotnet run --project examples/AdvancedConsoleWithDIExample/AdvancedConsoleWithDIExample.csproj
```
