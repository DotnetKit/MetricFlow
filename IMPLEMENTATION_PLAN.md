# MetricFlow: System.Diagnostics.Metrics & OpenTelemetry Backing Plan

## Executive Summary

MetricFlow currently operates as a standalone in-memory metric aggregation library. While its developer experience (DX) for scoped tracking, item rates, and cardinality protection is superior to raw BCL instrumentation, its isolation from the standard .NET observability ecosystem limits its adoption in modern microservices.

This implementation plan bridges MetricFlow with the .NET Base Class Library (`System.Diagnostics.Metrics`). By doing so, **MetricFlow becomes an ergonomic, safety-wrapped facade over standard .NET telemetry**, allowing automatic export to **OpenTelemetry (Prometheus, Grafana, Datadog, AWS CloudWatch, Azure Monitor)** and **`dotnet-counters`**, while preserving fast in-memory snapshots and cardinality protections.

---

## Architectural Blueprint

```mermaid
flowchart TD
    subgraph AppLayer ["Application Code (High-Level DX)"]
        Track["using (tracker.Track(\"ProcessOrder\"))"]
        Tags["SetTag(\"region\", \"eu\").SetItems(50)"]
    end

    subgraph MetricFlowCore ["DotnetKit.MetricFlow Engine"]
        MT[MetricTracker]
        CG[Cardinality Guard & Dimension Sanitizer]
        LocalSnapshots[(In-Memory Aggregations & Snapshots)]
    end

    subgraph BCLBridge ["System.Diagnostics.Metrics Bridge"]
        Meter["Meter: DotnetKit.MetricFlow.{Topic}"]
        Hist["Histogram: operation.duration (ms)"]
        OpsCounter["Counter: operation.total"]
        ItemCounter["Counter: operation.items"]
        ErrCounter["Counter: operation.exceptions"]
        ActiveGauge["ObservableGauge: operation.active"]
    end

    subgraph Exporters ["Standard .NET Observability Ecosystem"]
        DotnetCounters["dotnet-counters CLI"]
        OTel["OpenTelemetry .NET SDK"]
        Prometheus["Prometheus / Grafana"]
        Cloud["Azure Monitor / Datadog / OTLP"]
    end

    Track --> MT
    Tags --> MT
    MT --> CG
    CG --> LocalSnapshots
    CG -->|Publish Instruments with TagList| Meter
    
    Meter --> Hist
    Meter --> OpsCounter
    Meter --> ItemCounter
    Meter --> ErrCounter
    Meter --> ActiveGauge

    Meter -.-> DotnetCounters
    Meter -.-> OTel
    OTel --> Prometheus
    OTel --> Cloud
```

---

## Key Design Principles

1. **Zero Breaking Changes**: Existing APIs (`using (tracker.Track(...))`, `.In()`, `.Out()`, `.GetSnapshot()`, `.ToString()`) remain completely functional and binary-compatible.
2. **Cardinality Protection *Before* Emission**: High-cardinality tags are sanitized/rolled into `[Other]` *prior* to publishing to `System.Diagnostics.Metrics`, preventing Prometheus memory blowups.
3. **Opt-in / Configurable Metering**: Telemetry meters can be customized, disabled, or isolated per `Topic`.
4. **Zero Overhead for Local-Only Apps**: If no `MeterListener` or OpenTelemetry provider is attached, `System.Diagnostics.Metrics` operations are effectively no-ops.

---

## Phased Implementation Roadmap

### Phase 1: Core System.Diagnostics.Metrics Bridge (`DotnetKit.MetricFlow`)

#### 1.1 `MetricFlowMeterRegistry` & Meter Abstraction
- Create an internal or injectable `IMetricMeterBridge` managing `Meter` instances keyed by topic or options:
  - Meter naming pattern: `DotnetKit.MetricFlow.{Topic}` (configurable prefix).
  - Version matching assembly version.
- Manage lifecycle: implement `IDisposable` to properly dispose `Meter` instances when a tracker or factory is disposed.

#### 1.2 Instrument Mappings
Map core operations to standard BCL instruments:
| MetricFlow Concept | Instrument Type | Metric Name | Unit | Tags / Attributes |
|---|---|---|---|---|
| **DurationCounter** | `Histogram<double>` | `{metricName}.duration` | `ms` | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Execution Counts** | `Counter<long>` | `{metricName}.total` | `{operations}` | `operation`, `status` ("ok"/"error"), sanitized tags |
| **Throughput / Items**| `Counter<long>` | `{metricName}.items` | `{items}` | `operation`, sanitized tags |
| **ExceptionCounter** | `Counter<long>` | `{metricName}.exceptions` | `{exceptions}` | `operation`, `exception.type`, sanitized tags |
| **In-Flight / Concurrency** | `UpDownCounter<long>` | `{metricName}.active` | `{operations}` | `operation`, sanitized tags |

#### 1.3 High-Performance `TagList` Translation & Cardinality Filtering
- Implement zero-allocation / stack-allocated `TagList` conversions from MetricFlow tags and topic tags.
- Hook into `DimensionCounter`'s existing threshold logic:
  - If a tag key exceeds `MaxUniqueValues`, the tag value is clamped to `[Other]` before passing into `TagList`.
  - Provide an option `EnforceCardinalityLimitsOnMeters = true` (default: true).

#### 1.4 Dual-Dispatch in `MetricTrackerBase` / `CodeTracker`
- In `CodeTracker.Dispose()` and `MetricTrackerBase.Out()`:
  - Continue updating local in-memory counters (`DurationCounter`, `ExceptionCounter`, etc.).
  - Dispatch corresponding measurements to the instrument bridge (`Histogram.Record`, `Counter.Add`).

---

### Phase 2: OpenTelemetry Integration (`DotnetKit.MetricFlow.OpenTelemetry`)

#### 2.1 New Dedicated NuGet Package
- Project: `src/DotnetKit.MetricFlow.OpenTelemetry`
- Target: `net8.0;net10.0`
- Dependencies:
  - `DotnetKit.MetricFlow`
  - `OpenTelemetry` (v1.9+)

#### 2.2 Fluent OpenTelemetry Registration
Add extension methods for standard `MeterProviderBuilder`:
```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMetricFlowInstrumentation(options =>
            {
                // Optionally filter by topic wildcards
                options.MeterNamePattern = "DotnetKit.MetricFlow.*";
                options.RecordActiveOperations = true;
            })
            .AddOtlpExporter()
            .AddPrometheusExporter();
    });
```

#### 2.3 Distributed Tracing Correlation (`ActivitySource`)
- Option to automatically enrich current `Activity.Current` with MetricFlow scope details, or vice versa (copying `trace_id` and `span_id` into snapshot tags).

---

### Phase 3: AspNetCore Integration & dotnet-counters Diagnostics

#### 3.1 `dotnet-counters` Out-of-the-Box Support
Verify and document zero-config CLI usage:
```bash
# Monitor live in terminal without any external collector
dotnet-counters monitor -p <PID> --counters DotnetKit.MetricFlow.OrdersService
```

#### 3.2 Enhanced `/metrics/metricflow` Endpoint
- Retain the human-readable ASCII table / JSON snapshot endpoint for quick browser debugging.
- Add Prometheus-compatible exposition format endpoint: `/metrics` (or delegate directly to OpenTelemetry's Prometheus middleware).

---

## Detailed Task Breakdown

| # | Task | Project | Complexity |
|---|---|---|---|
| **1.1** | Add `MetricFlowMeterOptions` (MeterPrefix, Enabled, IncludeTopicTags) | `DotnetKit.MetricFlow` | Low |
| **1.2** | Implement `MetricFlowMeterBridge` wrapping `System.Diagnostics.Metrics.Meter` | `DotnetKit.MetricFlow` | Medium |
| **1.3** | Integrate `MeterBridge` into `MetricTrackerBase` & `CodeTracker` | `DotnetKit.MetricFlow` | Medium |
| **1.4** | Add TagList conversion with cardinality rollover protection | `DotnetKit.MetricFlow` | Medium |
| **1.5** | Unit & Integration tests for Meter instruments via `MeterListener` | `MetricFlow.Tests` | Medium |
| **2.1** | Create `DotnetKit.MetricFlow.OpenTelemetry` library & csproj | New Project | Low |
| **2.2** | Implement `AddMetricFlowInstrumentation` extension methods | `DotnetKit.MetricFlow.OpenTelemetry` | Low |
| **2.3** | End-to-end tests with OpenTelemetry exporter & in-memory reader | Test Project | Medium |
| **3.1** | Documentation & Example updates (`README.md`, `ROADMAP.md`, example apps) | Documentation | Low |

---

## Verification & Acceptance Criteria

1. **Standard `MeterListener` Validation**:
   - Tests assert that `tracker.Track("Checkout")` publishes a duration histogram record and an incremented count to a listener subscribed to `DotnetKit.MetricFlow.*`.
2. **Cardinality Protection Test**:
   - Generate 1,000 distinct tag values on a tracker with `MaxUniqueValues = 50`.
   - Assert that `System.Diagnostics.Metrics` records only at most 51 distinct tag combinations (the 50 distinct values + `[Other]`).
3. **Compatibility**:
   - All 123 existing unit tests continue to pass without modifications.
   - Benchmark showing $< 5\%$ allocation/time delta when meters are active.
4. **OpenTelemetry Export**:
   - Example project demonstrates streaming `MetricFlow` telemetry to Prometheus or OTLP collector.
