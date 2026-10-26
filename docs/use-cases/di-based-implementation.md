# DI-Based Implementation (Dependency Injection)

The **DI-Based Implementation** integrates MetricFlow into .NET applications utilizing standard Microsoft Dependency Injection (`IServiceCollection`). It enables clean separation of concerns, centralized configuration, and seamless injection of [`IMetricTracker`](xref:DotnetKit.MetricFlow.Abstractions.IMetricTracker) and [`IMetricFlow`](xref:DotnetKit.MetricFlow.Abstractions.IMetricFlow) into your application services and workers.

---

## Best For

- **Background Services & Daemons**: `IHostedService`, `BackgroundService`, and Worker Services.
- **Enterprise Applications**: Services utilizing dependency injection, options patterns, and structured logging.
- **Multi-Tenant Architectures**: Separate topic trackers per tenant, bounded context, or domain module.
- **Modular Microservices**: Decoupled service layers consuming telemetry via standard interfaces.

---

## Quick Start

Register MetricFlow during service configuration and inject [`IMetricTracker`](xref:DotnetKit.MetricFlow.Abstractions.IMetricTracker) into worker constructors:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// 1. Register MetricFlow with primary topic and counters
builder.Services.AddMetricFlow("BillingWorker", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["environment"] = builder.Environment.EnvironmentName;
        tags["cluster"] = "us-east-prod";
    })
    .AddThroughputCounter()
    .AddMemoryCounter()
    .AddExceptionCounter()
    .AddDimensionCounter("payment_channel");
})
// 2. Register an additional specialized topic tracker
.AddMetricTracker("AuditWorker", options =>
{
    options.AddTagsEnricher(tags => tags["tier"] = "internal-audit");
});

builder.Services.AddHostedService<PaymentWorkerService>();
builder.Services.AddTransient<AuditService>();

var host = builder.Build();
await host.RunAsync();
```

---

## Available Options & API Reference

### 1. Service Collection Registration Methods

| Extension Method | Description |
| :--- | :--- |
| `services.AddMetricFlow(string topic, Action<MetricFlowOptions>? configure = null)` | Registers MetricFlow with a default primary topic and returns an [`IMetricFlowBuilder`](xref:DotnetKit.MetricFlow.Abstractions.IMetricFlowBuilder). |
| `services.AddMetricFlow(Action<MetricFlowOptions> configure)` | Registers MetricFlow using configuration provided in the options action. |

### 2. Fluent Builder Methods (`IMetricFlowBuilder`)

| Builder Method | Description |
| :--- | :--- |
| `.AddMetricTracker(string topic, Action<MetricFlowOptions>? configure = null)` | Registers an additional named topic tracker, accessible via `[FromKeyedServices(topic)]` or `IMetricFlow.GetTracker(topic)`. |
| `.WithOpenTelemetry(...)` | Chains OpenTelemetry exporter and meter provider integration (requires `DotnetKit.MetricFlow.OpenTelemetry`). |

### 3. Core `MetricFlowOptions` Configuration

| Option Property / Method | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Topic` | `string` | `"Application"` | Topic identifier for the tracker. |
| `TopicTags` | `Dictionary<string, string>` | `null` | Contextual static tags attached at topic scope. |
| `AddTagsEnricher(Action<Dictionary<string, string>>)` | Method | Fluent | Chains tag enrichment delegates. |
| `SamplingRate` | `double?` | `1.0` | Probabilistic sampling rate between `0.0` and `1.0`. |
| `AutoAddExceptionCounter` | `bool` | `true` | Automatically captures uncaught runtime exceptions. |
| `AddThroughputCounter(string name)` | Method | Fluent | Tracks item throughput rates and batch sizes. |
| `AddMemoryCounter(string name)` | Method | Fluent | Measures heap allocation deltas per operation. |
| `AddFailureCounter(string name)` | Method | Fluent | Distinguishes logical business failures from exceptions. |
| `AddHierarchyCounter(string name)` | Method | Fluent | Enables async parent-child scope tracking and self-time. |
| `AddDimensionCounter(...)` | Method | Fluent | Aggregates distributions across custom business dimensions. |
| `ConfigureSinkSampling(...)` | Method | Fluent | Configures timer-free execution triggers for sinks. |
| `ConfigureMeters(...)` | Method | Fluent | Configures `System.Diagnostics.Metrics` and cardinality limits. |

### 4. Injected Service Interfaces

| Interface | Injection Style | Description |
| :--- | :--- | :--- |
| [`IMetricTracker`](xref:DotnetKit.MetricFlow.Abstractions.IMetricTracker) | `public MyService(IMetricTracker tracker)` | Injects the primary default topic tracker. |
| [`IMetricTracker`](xref:DotnetKit.MetricFlow.Abstractions.IMetricTracker) | `public MyService([FromKeyedServices("AuditWorker")] IMetricTracker auditTracker)` | Injects a specific keyed topic tracker. |
| [`IMetricFlow`](xref:DotnetKit.MetricFlow.Abstractions.IMetricFlow) | `public MyService(IMetricFlow metricFlow)` | Top-level coordinator providing access to all registered trackers (`Trackers`, `GetTracker(name)`, `DefaultTracker`). |

---

## Frequently Asked Questions (FAQ)

### Q: How do I inject distinct trackers into different worker services?

**A:** Use `.AddMetricTracker("TopicName")` during registration, then resolve it using .NET Keyed Services (`[FromKeyedServices("TopicName")]`):

```csharp
// DI Registration
builder.Services.AddMetricFlow("BillingTopic")
    .AddMetricTracker("AuditTopic");

// Primary worker injecting default tracker
public class BillingService(IMetricTracker billingTracker)
{
    public void Process()
    {
        using var scope = billingTracker.Track("ChargeSubscription");
    }
}

// Auxiliary service injecting keyed tracker
public class AuditService([FromKeyedServices("AuditTopic")] IMetricTracker auditTracker)
{
    public void Audit()
    {
        using var scope = auditTracker.Track("SecurityComplianceCheck");
    }
}
```

---

### Q: How can I inspect or report metrics across all registered trackers simultaneously?

**A:** Inject the top-level [`IMetricFlow`](xref:DotnetKit.MetricFlow.Abstractions.IMetricFlow) facade, which exposes all active trackers:

```csharp
public class TelemetryHealthReporter(IMetricFlow metricFlow)
{
    public void PrintAllSummaries()
    {
        foreach (var tracker in metricFlow.Trackers)
        {
            Console.WriteLine($"=== Topic: {tracker.Topic} ===");
            Console.WriteLine(tracker.ToString());
        }
    }
}
```

---

### Q: What is the service lifetime of `IMetricTracker` in the DI container?

**A:** Trackers are registered as **Singletons**. MetricFlow is engineered with lock-free atomic hot paths (`Interlocked`, immutable snapshot arrays, and `AsyncLocal`), making singleton trackers completely safe for concurrent access across thousands of parallel threads and background tasks without locking overhead.

---

### Q: How do I dynamically enrich topic tags with environment or configuration values?

**A:** Chain `AddTagsEnricher` within the options configuration delegate:

```csharp
builder.Services.AddMetricFlow("IngestionService", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["host.name"] = Environment.MachineName;
        tags["os.version"] = Environment.OSVersion.ToString();
        tags["app.version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    });
});
```

---

### Q: How can I dynamically enable or disable counters at runtime without restarting the app?

**A:** Pass an `ICounterConfigObservable` or use [`CounterConfigNotifier`](xref:DotnetKit.MetricFlow.Configuration.CounterConfigNotifier) on `options.ConfigObservable`:

```csharp
var notifier = new CounterConfigNotifier();

builder.Services.AddMetricFlow("DynamicService", options =>
{
    options.ConfigObservable = notifier;
    options.AddThroughputCounter();
    options.AddMemoryCounter();
});

// Later at runtime (e.g. from an admin endpoint or configuration change listener):
notifier.NotifyToggle("Throughput", enabled: false);
notifier.NotifyToggle("Throughput", enabled: true);
```
