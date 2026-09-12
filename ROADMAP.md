# MetricFlow Roadmap

The primary focus for upcoming MetricFlow development is seamless integration with **OpenTelemetry** and major **Cloud Providers**, allowing MetricFlow metrics and snapshots to flow directly into modern observability pipelines.

---

## 1. OpenTelemetry Integration

- **OpenTelemetry Metrics (`DotnetKit.MetricFlow.OpenTelemetry`)**:
  - Map MetricFlow counters and snapshots to `System.Diagnostics.Metrics` (`Meter`, `Counter`, `Histogram`).
  - OTLP export support to send telemetry to collectors, Prometheus, Grafana, and Jaeger.
- **Distributed Tracing Alignment**:
  - Correlate MetricFlow tracking scopes (`Track`, `TrackAction`) with OpenTelemetry `Activity` and trace contexts.

## 2. Cloud Provider Integrations

- **Azure Monitor / Application Insights**:
  - Exporter package for Azure Monitor metrics and telemetry.
- **AWS CloudWatch**:
  - Integration supporting CloudWatch metrics and Embedded Metric Format (EMF).
- **Google Cloud Monitoring**:
  - Exporter for Google Cloud Operations suite (formerly Stackdriver).

## 3. Push Exporter & Sinks

- **Periodic Push Exporter**:
  - Background worker to automatically harvest snapshots and dispatch to sinks at defined intervals.
- **Pluggable Sink Abstraction (`IMetricSink`)**:
  - Unified interface to stream metrics to custom endpoints and APM vendors (Datadog, New Relic, etc.).
