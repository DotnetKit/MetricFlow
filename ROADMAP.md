# MetricFlow Roadmap

The roadmap outlines the upcoming development milestones for MetricFlow, prioritized to establish reliable metric persistence and export pipelines alongside native OpenTelemetry observability.

---

## 1. Metric Persistence

- **Local & Durable Storage**:
  - Buffer and persist raw metric points and aggregated snapshots locally (file-based, SQLite, or embedded stores) to prevent data loss during network interruptions or service restarts.
- **SQLite Time-Series Storage (`DotnetKit.MetricFlow.Persistence.Sqlite`)**:
  - Compact historical snapshot persistence supporting offline trend queries, local dashboards, and audit logs.
- **Resilient Buffering & WAL (Write-Ahead Log)**:
  - High-throughput circular memory buffer with fallback to disk for resilient, non-blocking telemetry collection in edge/disconnected environments.
- **Snapshot History & Querying**:
  - In-process or persisted time-series snapshot storage enabling historical trend queries and offline analysis.

---

## 2. Pluggable Sinks & Observable Capture (`IMetricSink`)

- **Pluggable Sink Abstraction (`IMetricSink`)**:
  - [x] Core interface and batching pipeline (`IMetricSink`) for dispatching aggregated snapshots to destinations.
  - [x] Reactive / Observable pattern (`ObservableMetricSink` implementing `IObservable<IReadOnlyList<IMetricSnapshot>>`) for streaming snapshots to observers.
- **Timer-Free Sampling Triggers**:
  - [x] Stride sampling: flush snapshots every $N$ executions (e.g. 50 or 100 operations) directly during tracking lifecycle.
  - [x] Tail / Outlier sampling: automatically emit snapshots on operation failure (`failed == true`, exception thrown) or when latency exceeds duration thresholds.
  - [x] Probabilistic sampling: sample a configurable percentage of operations without background timer overhead.
  - [x] Zero background thread requirement: fully compatible with serverless (AWS Lambda), CLI tools, and batch jobs.
- **Console Log Sink (`ConsoleMetricSink`)**:
  - [x] High-performance, structured console log sink writing human-readable metric snapshots to `Console.Out` or custom `TextWriter` with optional ANSI colorization.
- **Centralized Logging Sink (`ILogger` / Serilog)**:
  - Dedicated sink translating snapshots into structured `ILogger` events for Serilog, Seq, Loki, and ELK stack integration.
- **Periodic Push Exporter (Optional)**:
  - Opt-in background worker to periodically harvest active snapshots and flush them to registered sinks at scheduled time intervals.

---

## 3. Sinks and Adapters for OpenTelemetry & Cloud Providers

- **OpenTelemetry Integration (`DotnetKit.MetricFlow.OpenTelemetry`)** *(Completed in v1.0.51)*:
  - [x] Map MetricFlow counters and snapshots to `System.Diagnostics.Metrics` (`Meter`, `Counter`, `Histogram`, `UpDownCounter`).
  - [x] Native OTLP export via OpenTelemetry SDK pipeline (Prometheus, Grafana, OTLP collectors, `dotnet-counters`).
  - [x] Trace context correlation: ambient OpenTelemetry `Activity.Current` capture (`WithTraceContext`) and `ActivitySource` registration.
  - [x] Cardinality protection guard clamping high-cardinality tags before emission.
- **Cloud Provider Integration Strategy**:
  - *Standard Strategy*: In modern cloud-native architectures, exporting to **AWS CloudWatch (EMF/OTLP)**, **Azure Monitor (Application Insights)**, **Google Cloud Monitoring**, and **Datadog** is natively handled through OpenTelemetry OTLP exporters and `System.Diagnostics.Metrics` integration without needing proprietary SDK clients in MetricFlow core.
  - *Dedicated Adapters*: Provide lightweight helper extensions or documentation guides for wiring MetricFlow's OTel meters directly to vendor-specific OpenTelemetry distribution packages (e.g., `Azure.Monitor.OpenTelemetry.AspNetCore`, AWS EMF exporter).


