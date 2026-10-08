# DotnetKit.MetricFlow.OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/DotnetKit.MetricFlow.OpenTelemetry)](https://www.nuget.org/packages/DotnetKit.MetricFlow.OpenTelemetry)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

OpenTelemetry integration package for [MetricFlow](https://github.com/DotnetKit/MetricFlow), providing turnkey integration between MetricFlow trackers and the standard OpenTelemetry .NET ecosystem.

## Features

- **Fluent Builder Integration**: Effortlessly wire OpenTelemetry metrics and tracing via `.WithOpenTelemetry()` directly on `IMetricFlowBuilder`.
- **MeterProvider Instrumentation**: Turnkey `.AddMetricFlowInstrumentation()` extension for existing OpenTelemetry `MeterProviderBuilder` setups.
- **BCL Instrument Mapping**: Zero-overhead mapping from MetricFlow counters to standard .NET BCL `System.Diagnostics.Metrics` instruments (`Histogram`, `Counter`, `UpDownCounter`).
- **Distributed Tracing Correlation**: Ambient trace correlation via `.WithTraceContext()`, attaching active `trace_id` and `span_id` to metric measurements.
- **Cardinality Protection**: Inherits MetricFlow's `TagCardinalityGuard` to safeguard downstream APM backends (Prometheus, Datadog, OTLP) from high-cardinality label explosions.

## Installation

Install via the .NET CLI:

```sh
dotnet add package DotnetKit.MetricFlow.OpenTelemetry
```

Or via Package Manager:

```powershell
Install-Package DotnetKit.MetricFlow.OpenTelemetry
```

## Quick Start

### 1. Fluent Builder Setup (`WithOpenTelemetry`)

Configure OpenTelemetry metrics and exporters directly within your MetricFlow DI registration:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.OpenTelemetry;
using OpenTelemetry.Metrics;

services.AddMetricFlow("Billing", options =>
{
    options.AddThroughputCounter();
})
.WithOpenTelemetry(otel =>
{
    otel.WithMetrics(metrics =>
    {
        // Export to any OpenTelemetry collector or exporter
        metrics.AddOtlpExporter()
               .AddPrometheusExporter();
    });

    otel.WithTracing();
});
```

### 2. Standalone OpenTelemetry Instrumentation (`AddMetricFlowInstrumentation`)

If your application already configures OpenTelemetry via `services.AddOpenTelemetry()`, subscribe directly to MetricFlow meters:

```csharp
using DotnetKit.MetricFlow.OpenTelemetry;
using OpenTelemetry.Metrics;

services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMetricFlowInstrumentation(options =>
        {
            // Subscribe to all topics (default) or specify target topics
            options.Topics.Add("Billing");
        })
        .AddOtlpExporter();
    });
```

## Standard BCL Instruments Mapped

MetricFlow automatically translates tracked operations into standard .NET BCL instruments:

| MetricFlow Concept          | Instrument Type       | Metric Name               | Unit           | Tags / Attributes                                    |
| --------------------------- | --------------------- | ------------------------- | -------------- | ---------------------------------------------------- |
| **DurationCounter**         | `Histogram<double>`   | `{metricName}.duration`   | `ms`           | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Execution Counts**        | `Counter<long>`       | `{metricName}.total`      | `{operations}` | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Throughput / Items**      | `Counter<long>`       | `{metricName}.items`      | `{items}`      | `operation`, sanitized tags                          |
| **ExceptionCounter**        | `Counter<long>`       | `{metricName}.exceptions` | `{exceptions}` | `operation`, `exception.type`, sanitized tags        |
| **In-Flight / Concurrency** | `UpDownCounter<long>` | `{metricName}.active`     | `{operations}` | `operation`, sanitized tags                          |

## Ambient Distributed Tracing Correlation

Enrich scope tags with the ambient OpenTelemetry `Activity.Current` trace and span IDs using `WithTraceContext()`:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.OpenTelemetry;

var tags = new Dictionary<string, string> { ["region"] = "eu" }.WithTraceContext();

using (tracker.Track("ProcessOrder", tags))
{
    // Metric measurements now carry trace_id and span_id attributes
}
```

## Example Project

For a complete working example demonstrating OpenTelemetry export to console, cardinality safeguards, item throughput, and ambient trace correlation, see:
- [OpenTelemetryConsoleExample](../../examples/OpenTelemetryConsoleExample)

## Related Documentation

- [MetricFlow Core README](../../README.md)
- [MetricFlow Architecture Documentation](../../ARCHITECTURE.md)
