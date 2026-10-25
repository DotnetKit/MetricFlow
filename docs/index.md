# MetricFlow Documentation

Welcome to the **MetricFlow** documentation!

Explore the guides, use cases, and references below:

- [Getting Started](../README.md)
- [Architecture](../ARCHITECTURE.md)
- [Roadmap](../ROADMAP.md)
- [Changelog](../CHANGELOG.md)
- [API Reference](xref:DotnetKit.MetricFlow)

## Use Cases & Integration Guides

- [Simple Implementation](use-cases/simple-implementation.md): Targeted profiling, CLI jobs, micro-benchmarks with standalone `MetricTracker`.
- [DI-Based Implementation](use-cases/di-based-implementation.md): Background workers, multi-tenant services with standard `IServiceCollection`.
- [Console Log Sink & Triggers](use-cases/console-log-sink.md): Real-time formatted console logging with timer-free execution triggers.
- [Hierarchical Scope Trees](use-cases/hierarchical-scope-trees.md): Nested parent-child flows, exclusive self-time, and distributed trace bridging.
- [Logger Sink (Serilog / ILogger)](use-cases/logger-sink.md): Centralized structured logging and APM log ingestion.
- [Web Implementation](use-cases/web-implementation.md): ASP.NET Core middleware, route pattern resolution, and `/metrics` exposition.
- [OpenTelemetry & Observability](use-cases/opentelemetry-observability.md): Prometheus, Grafana, Datadog, OTLP collectors, and CLI diagnostics.
