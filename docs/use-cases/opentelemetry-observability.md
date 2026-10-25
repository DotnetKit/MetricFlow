# OpenTelemetry & Observability

The **OpenTelemetry & Observability** pattern bridges MetricFlow into the standard OpenTelemetry .NET ecosystem via [`DotnetKit.MetricFlow.OpenTelemetry`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow.OpenTelemetry/MetricFlowInstrumentationOptions.cs). It maps MetricFlow counters directly to native .NET BCL `System.Diagnostics.Metrics` instruments, correlates metrics with ambient distributed traces, provides built-in tag cardinality protection, and allows live terminal inspection via `dotnet-counters`.

---

## Best For

- **Cloud-Native Observability**: Exporting metrics to Prometheus, Grafana Mimir, Datadog, New Relic, Dynatrace, or AWS CloudWatch via standard OTLP collectors.
- **Distributed Tracing Correlation**: Linking metric measurements to active distributed trace spans (`trace_id` and `span_id`).
- **High-Throughput APM Backends**: Preventing tag cardinality explosions from degrading backend storage or driving up APM billings.
- **Local Developer Diagnostics**: Live real-time inspection in developer terminals using `dotnet-counters monitor`.

---

## Quick Start

Install `DotnetKit.MetricFlow.OpenTelemetry` and configure both MetricFlow and OpenTelemetry in your dependency injection setup using `.WithOpenTelemetry()`:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

var services = new ServiceCollection();

// 1. Configure MetricFlow with OpenTelemetry fluent builder
services.AddMetricFlow("BillingService", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["environment"] = "Production";
        tags["datacenter"] = "us-east-1";
    })
    .AddThroughputCounter();

    // Safeguard Prometheus / Datadog from high-cardinality tag explosions
    options.ConfigureMeters(m =>
    {
        m.MaxUniqueTagValues = 100;
        m.OverflowBucket = "[Other]";
    });
})
.WithOpenTelemetry(otel =>
{
    otel.WithMetrics(metrics =>
    {
        // Export to OTLP collector, Prometheus, or Console
        metrics.AddConsoleExporter();
    })
    .WithTraceContext(); // Attach ambient trace_id and span_id
});

using var provider = services.BuildServiceProvider();
```

---

## Available Options & API Reference

### 1. `MetricFlowInstrumentationOptions`

[`MetricFlowInstrumentationOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow.OpenTelemetry/MetricFlowInstrumentationOptions.cs) configures OpenTelemetry meter subscriptions:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `MeterNamePattern` | `string` | `"DotnetKit.MetricFlow.*"` | Wildcard pattern registered with the OpenTelemetry `MeterProviderBuilder`. |
| `MeterPrefix` | `string` | `"DotnetKit.MetricFlow"` | Base prefix used when expanding specific topic meter names (`$"{prefix}.{topic}"`). |
| `Topics` | `IList<string>` | `[]` | Specific topics to register. If empty, `MeterNamePattern` is used. |
| `RecordActiveOperations`| `bool` | `true` | When true, tracks in-flight active operations using an `UpDownCounter`. |

### 2. `MetricFlowMeterOptions` (Cardinality Guard & Trace Context)

[`MetricFlowMeterOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Meters/MetricFlowMeterOptions.cs) configures low-level BCL meter behavior and safeguards:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Enabled` | `bool` | `true` | Enables or disables emission to native `System.Diagnostics.Metrics`. |
| `MaxUniqueTagValues` | `int` | `250` | Maximum unique values per tag key before routing new values to `OverflowBucket`. |
| `OverflowBucket` | `string` | `"[Other]"` | Replacement label value when cardinality threshold is exceeded. |
| `EnableTraceContext` | `bool` | `false` | When true, automatically attaches ambient `trace_id` and `span_id` as metric tags. |

### 3. Native BCL Instrument Mapping

| MetricFlow Counter | BCL Instrument Type | Instrument Unit |
| :--- | :--- | :--- |
| **Duration** | `Histogram<double>` | `ms` |
| **Throughput / Items** | `Counter<long>` | `{items}` |
| **Active Operations** | `UpDownCounter<long>` | `{operations}` |
| **Memory Allocation** | `Histogram<long>` | `By` (Bytes) |
| **Exceptions & Failures** | `Counter<long>` | `{errors}` |

---

## Frequently Asked Questions (FAQ)

### Q: How do I prevent high-cardinality tag explosions from exhausting Prometheus or Datadog memory?

**A:** MetricFlow includes a built-in `TagCardinalityGuard`. Configure it via `options.ConfigureMeters`:

```csharp
options.ConfigureMeters(m =>
{
    // Limit any single tag key (e.g. user_id or error_code) to 50 unique values
    m.MaxUniqueTagValues = 50;

    // All subsequent distinct tag values are grouped into this safe bucket
    m.OverflowBucket = "[HighCardinalityOverflow]";
});
```

---

### Q: How do I attach ambient distributed trace context (`trace_id` and `span_id`) to metric measurements?

**A:** Call `.WithTraceContext()` on the OpenTelemetry fluent builder:

```csharp
services.AddMetricFlow("OrderService")
    .WithOpenTelemetry(otel =>
    {
        otel.WithTraceContext(); // Injects trace_id and span_id into every measurement
    });
```

---

### Q: How do I export MetricFlow metrics to an OTLP Collector (Datadog, Grafana Mimir, New Relic)?

**A:** Chain OpenTelemetry's standard `AddOtlpExporter()` within the metrics configuration:

```csharp
using OpenTelemetry.Metrics;

services.AddMetricFlow("BillingService")
    .WithOpenTelemetry(otel =>
    {
        otel.WithMetrics(metrics =>
        {
            metrics.AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri("http://localhost:4317"); // OTLP gRPC endpoint
            });
        });
    });
```

---

### Q: How can I live-monitor MetricFlow instruments in my local terminal using `dotnet-counters`?

**A:** Because MetricFlow registers native `System.Diagnostics.Metrics` meters, you can use the standard .NET CLI diagnostics tool without installing any SDKs or forwarders:

```sh
# 1. List running .NET processes
dotnet-counters ps

# 2. Monitor MetricFlow meter in real-time
dotnet-counters monitor --process-id <PID> --counters DotnetKit.MetricFlow.BillingService
```

You will see real-time terminal gauges showing execution counts, durations, and throughput rates.

---

### Q: How do I integrate MetricFlow into an existing OpenTelemetry setup?

**A:** If your application already configures OpenTelemetry via `AddOpenTelemetry().WithMetrics(...)`, use the `.AddMetricFlowInstrumentation()` extension:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMetricFlowInstrumentation(options =>
        {
            options.Topics.Add("BillingService");
            options.Topics.Add("OrderService");
        })
        .AddPrometheusExporter();
    });
```
