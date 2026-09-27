# MetricFlow Roadmap

The roadmap outlines the upcoming development milestones for MetricFlow, prioritized to establish reliable metric persistence and export pipelines before integrating external observability platforms.

---

## 1. Metric Persistence

- **Local & Durable Storage**:
  - Buffer and persist raw metric points and aggregated snapshots locally (file-based, SQLite, or embedded stores) to prevent data loss during network interruptions or service restarts.
- **Resilient Buffering & WAL (Write-Ahead Log)**:
  - High-throughput circular memory buffer with fallback to disk for resilient, non-blocking telemetry collection.
- **Snapshot History & Querying**:
  - In-process or persisted time-series snapshot storage enabling historical trend queries and offline analysis.

---

## 2. Push Exporter & Sinks

- **Pluggable Sink Abstraction (`IMetricSink`)**:
  - Core interfaces and pipelines for dispatching formatted snapshots and metrics to various destinations.
  - Support for batching, retry policies, backoff strategies, and dead-letter queues.
- **Periodic Push Exporter**:
  - Configurable background worker to periodically harvest active snapshots and flush them to registered sinks.
- **Custom Destination Extensions**:
  - Extension points for streaming metrics to custom endpoints, webhooks, or log-based sinks.

---

## 3. Sinks and Adapters for OpenTelemetry & Cloud Providers

- **OpenTelemetry Integration (`DotnetKit.MetricFlow.OpenTelemetry`)** *(Completed in v1.0.51)*:
  - [x] Map MetricFlow counters and snapshots to `System.Diagnostics.Metrics` (`Meter`, `Counter`, `Histogram`, `UpDownCounter`).
  - [x] Native OTLP export via OpenTelemetry SDK pipeline (Prometheus, Grafana, OTLP collectors, `dotnet-counters`).
  - [x] Trace context correlation: ambient OpenTelemetry `Activity.Current` capture (`WithTraceContext`) and `ActivitySource` registration.
  - [x] Cardinality protection guard clamping high-cardinality tags before emission.
- **Cloud Provider Sinks & Adapters**:
  - **Azure Monitor / Application Insights**: Dedicated exporter for Azure Monitor metrics and Application Insights custom metrics.
  - **AWS CloudWatch**: Exporter supporting CloudWatch metrics and Embedded Metric Format (EMF).
  - **Google Cloud Monitoring**: Exporter for Google Cloud Operations suite (Cloud Monitoring).
  - **APM Vendors**: Adapters for Datadog, New Relic, and other industry APMs.

