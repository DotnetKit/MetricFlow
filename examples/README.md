# MetricFlow Examples

This directory contains executable examples demonstrating different use cases and integration patterns for **MetricFlow**.

---

## Example Catalog

| Project / Directory | Description | Key Features |
| :--- | :--- | :--- |
| [**BasicConsoleExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/BasicConsoleExample/README.md) | Minimal zero-configuration console application. | Scoped tracking (`using tracker.Track(...)`), delegate tracking (`tracker.TrackAction(...)`), topic tags, snapshot string formatting. |
| [**AdvancedConsoleExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/AdvancedConsoleExample/README.md) | Full multi-counter standalone telemetry pipeline. | `ThroughputCounter`, `ExceptionCounter`, `MemoryCounter`, `DimensionCounter`, upfront batch tracking (`TrackItems`), dynamic sizing (`scope.SetItems`), `[CallerMemberName]`, programmatic snapshot querying. |
| [**AdvancedConsoleWithDIExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/AdvancedConsoleWithDIExample/README.md) | Microsoft Dependency Injection in worker/console apps. | `services.AddMetricFlow()`, `AddTagsEnricher()`, chained secondary topics (`AddMetricTracker()`), keyed DI (`[FromKeyedServices]`), top-level facade (`IMetricFlow`). |
| [**ConsoleSinkExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/ConsoleSinkExample/README.md) | Structured console log sink with timer-free triggers. | `ConsoleMetricSink`, `ConsoleMetricSinkOptions` (ANSI colors, prefixes, timestamps), `MetricSinkTriggerOptions` (stride, latency threshold, and failure triggers), DI & standalone usage, manual `FlushSinks()`. |
| [**LoggerSinkExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/LoggerSinkExample/README.md) | Structured ILogger sink integrated with Serilog. | `LoggerMetricSink`, `LoggerMetricSinkOptions` (category, dynamic log level elevation on failures), `ILogger` abstraction, Serilog integration, lifecycle triggers. |
| [**OpenTelemetryConsoleExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/OpenTelemetryConsoleExample/README.md) | Native OpenTelemetry .NET bridge integration. | `DotnetKit.MetricFlow.OpenTelemetry`, `.WithOpenTelemetry()`, standard `System.Diagnostics.Metrics` bridge (`MeterProvider`), tag cardinality protection (`MaxUniqueTagValues`), `dotnet-counters` live monitoring. |
| [**WebApiExample**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/WebApiExample/README.md) | ASP.NET Core Web API with middleware and metrics endpoint. | `DotnetKit.MetricFlow.AspNetCore`, `app.UseMetricFlow()` turnkey middleware, `AddHttpTagsEnricher()`, Minimal APIs, keyed injection, metrics endpoint (`app.MapMetricFlow()`). |
| [**CustomCounters**](file:///Users/evomac1/gh/DotnetKit/MetricFlow/examples/CustomCounters/README.md) | Authoring domain-specific custom counters and trackers. | `CounterBase<TState>` state token lifecycle (`OnIn`, `OnOut`), `MetricTrackerBase` subclassing, custom snapshot reporting. |

---

## Running Any Example

All runnable examples can be executed directly from the repository root:

```bash
# Basic console example
dotnet run --project examples/BasicConsoleExample/BasicConsoleExample.csproj

# Advanced multi-counter console example
dotnet run --project examples/AdvancedConsoleExample/AdvancedConsoleExample.csproj

# Dependency Injection console example
dotnet run --project examples/AdvancedConsoleWithDIExample/AdvancedConsoleWithDIExample.csproj

# Structured console log sink example
dotnet run --project examples/ConsoleSinkExample/ConsoleSinkExample.csproj

# Structured ILogger sink example (Serilog)
dotnet run --project examples/LoggerSinkExample/LoggerSinkExample.csproj

# OpenTelemetry integration example
dotnet run --project examples/OpenTelemetryConsoleExample/OpenTelemetryConsoleExample.csproj

# Web API example
dotnet run --project examples/WebApiExample/WebApiExample.csproj
```
