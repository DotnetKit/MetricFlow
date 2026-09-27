# Advanced Console Example with Dependency Injection (DI)

This example demonstrates how to integrate `MetricFlow` into a console application, daemon, or worker service using standard Microsoft Dependency Injection (`Microsoft.Extensions.DependencyInjection`).

## Highlights

- **Dependency Injection Setup:** Register MetricFlow in a console app without requiring ASP.NET Core.
- **`AddTagsEnricher` Helper:** Fluent configuration of topic-level metadata tags (`tenant_id`, `session_id`, `environment`).
- **Multi-Counter Pipeline:** Chains `ThroughputCounter`, `ExceptionCounter`, `MemoryCounter`, and `DimensionCounter`.
- **Multi-Topic Fluent Registration (`AddMetricTracker`):** Registers an isolated secondary topic tracker (`AuditWorker`) in the same container.
- **Keyed Services (`[FromKeyedServices("...")]`):** Direct injection of named topic trackers into domain services.
- **Top-Level Facade (`IMetricFlow`):** Unified reporting and inspection across all registered trackers.

---

## Code Overview

### 1. DI Container Registration

```csharp
var services = new ServiceCollection();

// Configure root MetricFlow with topic, enriched tags, and counters
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
// Fluently chain a secondary topic tracker
.AddMetricTracker("AuditWorker", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["role"] = "security-auditor";
        tags["tier"] = "internal";
    });
});

// Register worker services
services.AddTransient<BatchProcessorService>();
services.AddTransient<AuditService>();
```

---

### 2. Service Injections

#### Primary Service (Default `IMetricTracker`)
```csharp
public class BatchProcessorService(IMetricTracker tracker)
{
    public async Task RunAsync()
    {
        using var scope = tracker.Track("BatchTask");
        // ...
    }
}
```

#### Keyed Service Injection (`[FromKeyedServices]`)
```csharp
public class AuditService([FromKeyedServices("AuditWorker")] IMetricTracker auditTracker)
{
    public async Task RunAuditAsync()
    {
        using var scope = auditTracker.Track("SecurityComplianceCheck");
        // ...
    }
}
```

---

### 3. Unified Reporting via `IMetricFlow`

```csharp
var metricFlow = provider.GetRequiredService<IMetricFlow>();

foreach (var tracker in metricFlow.Trackers)
{
    Console.WriteLine(tracker.ToString());
}
```

---

## Running the Example

```bash
dotnet run --project examples/AdvancedConsoleWithDIExample/AdvancedConsoleWithDIExample.csproj
```
