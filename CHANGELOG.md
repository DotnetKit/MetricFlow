# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.54] - 2026-10-09

### Added

- **Native Threshold Color Coding & Dynamic Color Selectors (`ConsoleMetricSinkOptions`)**:
  - Declarative threshold rules via `opt.AddThreshold<DurationSnapshot>(warn, critical, ...)` to color-code snapshots based on latency thresholds (warning in DarkYellow/Orange, critical in Red, normal in Green).
  - Generalized threshold overloads for `TimeSpan`, `double`, `long`, and custom predicate rules across snapshot types (`FailureSnapshot`, `ExceptionSnapshot`, `MemorySnapshot`, `ThroughputSnapshot`).
  - Dynamic pattern-matching color selection via `opt.ColorSelector = snapshot => snapshot switch { ... }`.
  - ANSI color mapping utility (`ConsoleMetricSink.ToAnsi(ConsoleColor)`) with full color formatting on target badges and primary metrics.

### Fixed

- **XML Documentation Packaging**:
  - Enabled `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in exported project files (`DotnetKit.MetricFlow`, `DotnetKit.MetricFlow.AspNetCore`, and `DotnetKit.MetricFlow.OpenTelemetry`).
  - Ensures compiler embeds triple-slash XML doc comments (`/// <summary>`) in the generated NuGet packages for developer IntelliSense and API documentation.

---

## [1.0.53] - 2026-10-07

### Added

- **Pluggable Metric Sink Pipeline (`IMetricSink`)**:
  - Core `IMetricSink` abstraction supporting synchronous `Emit(IReadOnlyList<IMetricSnapshot>)` and asynchronous `EmitAsync(...)` with bidirectional default interface implementations.
  - In-process snapshot dispatching for centralized logging, file writes, and local persistence.
- **Reactive Observable Pattern (`ObservableMetricSink`)**:
  - Implementation of `IMetricSink` and `IObservable<IReadOnlyList<IMetricSnapshot>>` enabling standard reactive subscriptions (`sink.Subscribe(...)`) for piping metric snapshots to Rx.NET queries, event channels, or custom log writers.
- **Timer-Free Sampling Triggers (`MetricSinkTriggerOptions`)**:
  - First-class support for execution-lifecycle triggers evaluated during operation completion (`Out` / `Dispose`), completely avoiding mandatory background timers or polling threads:
    - *Stride Sampling*: Emit snapshots every $N$ executions (`EmitEveryNExecutions`).
    - *Tail / Outlier Sampling*: Immediately emit on operation failure (`EmitOnFailure`) or when duration exceeds latency thresholds (`EmitOnSlowDurationThreshold`).
    - *Probabilistic Sampling*: Random percentage sampling (`EmitSampleRate`).
  - Zero idle CPU and thread overhead; ideal for serverless (AWS Lambda), CLI utilities, and short-lived batch jobs.
- **Structured Console Log Sink (`ConsoleMetricSink` & `ConsoleMetricSinkOptions`)**:
  - High-performance console sink formatting metric snapshots into human-readable single-line structured log entries.
  - Configurable ANSI colorization (`Colorize`), timestamps, custom prefixes, and redirection to any `TextWriter` for testing.
  - Dedicated formatting for throughput rates, execution counts, and failure rates.
- **Fluent Builder & DI Extensions**:
  - Registered sinks on `MetricFlowOptions`: `options.AddSink(...)`, `options.AddConsoleSink(...)`, `options.AddObservableSink(...)`, and `options.ConfigureSinkTriggers(...)`.
  - Fluent builder chaining: `builder.AddConsoleSink(...)` and `builder.AddSink(...)`.
  - Automatic dependency injection discovery: automatically resolves any registered `IMetricSink` services from the DI container into metric trackers.
  - Manual and asynchronous flush support: `tracker.FlushSinks()` and `tracker.FlushSinksAsync()`.
- **Roadmap Updates (`ROADMAP.md`)**:
  - Updated Section 2 to reflect `IMetricSink`, `ObservableMetricSink`, timer-free sampling, and the console sink.
  - Realigned Section 3 to clarify that cloud APMs (AWS CloudWatch, Azure Monitor, Datadog, Prometheus) are natively handled via OpenTelemetry and `System.Diagnostics.Metrics`.

---

## [1.0.52] - 2026-10-03

### Added

- **Failure Counter & Snapshot (`FailureCounter` & `FailureSnapshot`)**:
  - Dedicated counter distinguishing logical failures (`failed: true`) from unhandled exception failures.
  - Computes `TotalOperations`, `TotalFailures`, `LogicalFailures`, `ExceptionFailures`, `FailureRate`, `LogicalFailureRate`, and `ExceptionFailureRate`.
  - Fluent registration helper `options.AddFailureCounter(name)` on `MetricFlowOptions`.
- **Strongly-Typed Snapshot Retrieval Extensions (`TrackerCounterExtensions`)**:
  - Generic retrieval APIs:
    - `tracker.GetSnapshot<TSnapshot>(metricName, counterName)`
    - `tracker.GetSnapshots<TSnapshot>(metricName)`
    - `source.GetAllSnapshots<TSnapshot>()`
  - Strongly-typed convenience extension methods:
    - `tracker.GetDurationSnapshot(metricName)`
    - `tracker.GetThroughputSnapshot(metricName)`
    - `tracker.GetExceptionSnapshot(metricName)`
    - `tracker.GetMemorySnapshot(metricName)`
    - `tracker.GetFailureSnapshot(metricName)`
    - `tracker.GetDimensionSnapshot(metricName, dimensionName)`

### Changed (Breaking Changes)

- **Standardized Snapshot Retrieval Naming**:
  - Renamed `GetThroughputValues(...)` to `GetThroughputSnapshot(...)` for consistency with snapshot terminology.
  - Renamed `GetDimensionValues(...)` to `GetDimensionSnapshot(...)`.
  - Updated example applications, documentation, and middleware to use the unified `*Snapshot` extension methods.

---

## [1.0.51] - 2026-09-27



### Added

- **Core `System.Diagnostics.Metrics` Bridge (`IMetricMeterBridge` & `MetricFlowMeterBridge`)**:
  - Direct integration with .NET Base Class Library (`System.Diagnostics.Metrics.Meter`) exposing standard instruments.
  - Duration Histogram (`{metricName}.duration` in milliseconds) capturing execution timings with `status` ("ok"/"error") and sanitized tags.
  - Operation Counter (`{metricName}.total` with `{operations}` unit) tracking execution volumes.
  - Throughput / Items Counter (`{metricName}.items` with `{items}` unit) tracking batch and processed items.
  - Exception Counter (`{metricName}.exceptions`) tracking failures with `exception.type`.
  - In-Flight Concurrency Counter (`{metricName}.active` `UpDownCounter<long>`) incrementing on operation start and decrementing on completion with matching tag lists.
- **Cardinality Protection & Sanitization (`TagCardinalityGuard`)**:
  - Automatic thread-safe cardinality enforcement preventing unbounded memory growth in metric exporters.
  - Enforces `MaxUniqueTagValues` per tag key (defaults to 250), rolling distinct values exceeding thresholds into `[Other]`.
  - Configurable per-tag cardinality limits (`TagCardinalityLimits`).
- **Meter Options & Multi-Topic Registry (`MetricFlowMeterOptions` & `MetricFlowMeterRegistry`)**:
  - Fluent configuration on `MetricFlowOptions`: `options.ConfigureMeters(...)` and `options.EnableMeters(...)`.
  - Configurable `MeterPrefix` (defaults to `"DotnetKit.MetricFlow"`), producing meters named `DotnetKit.MetricFlow.{Topic}`.
  - Support for both `PerMetricName` (default) and `SharedOperation` naming conventions.
  - Registered `MetricFlowMeterRegistry` in Microsoft DI, automatically injecting topic-scoped meter bridges into keyed and default trackers.
- **Lifecycle & Resource Cleanup**:
  - `IDisposable` support on `MetricTrackerBase`, `MetricTracker`, `MetricFlowRegistry`, and `IMetricMeterBridge` to ensure clean disposal of `Meter` instances on application shutdown.
- **OpenTelemetry Integration Package (`DotnetKit.MetricFlow.OpenTelemetry`)**:
  - Unified fluent integration on `IMetricFlowBuilder` via `.WithOpenTelemetry(otel => ...)` preserving the exact `AddMetricFlow(...)` developer experience while configuring OpenTelemetry metrics and tracing in a single chained call.
  - Sub-fluent builder abstraction (`IMetricFlowOpenTelemetryBuilder` & `MetricFlowOpenTelemetryBuilder`) implementing `IFluentBuilder<IServiceCollection>` with `WithMetrics(...)` (and `ConfigureMetrics(...)`), `WithTracing(...)` (and `ConfigureTracing(...)`), and `ConfigureInstrumentation(...)`.
  - Factory helpers `MetricFlowTelemetry.CreateMeterProvider(...)` and `MetricFlowTelemetry.CreateMeterProviderBuilder(...)` abstracting `Sdk.CreateMeterProviderBuilder()` and automated MetricFlow meter binding in standalone and console applications.
  - Low-level `.AddMetricFlowInstrumentation(...)` extensions on standard OpenTelemetry `MeterProviderBuilder` and `TracerProviderBuilder`.
  - Automated subscription to MetricFlow meters by pattern (`DotnetKit.MetricFlow.*`) or explicit topic lists.
  - Seamless export to any OpenTelemetry-compatible collector (Prometheus, OTLP, Grafana, Datadog, AWS CloudWatch, Azure Monitor).
  - Distributed tracing correlation via `WithTraceContext()` attaching `trace_id` and `span_id` to metrics, and `AddMetricFlowInstrumentation()` on `TracerProviderBuilder`.
  - Comprehensive integration test suite (`MetricFlow.OpenTelemetry.Tests`) validating DI fluent sub-builder, standalone telemetry factory, in-memory exporter reader, and trace correlation.
- **Automated Tests**:
  - Comprehensive unit and integration tests using `MeterListener` in `MetricMeterBridgeTests` and in-memory OpenTelemetry reader in `MetricFlow.OpenTelemetry.Tests`.

---

## [1.0.5] - 2026-09-27

### Added

- **Multi-Topic Architecture & Top-Level Facade (`IMetricFlow`)**:
  - Central `IMetricFlow` facade and `MetricFlowRegistry` for managing multiple metric topics within a single application.
  - Dynamic topic retrieval by name (`_metricFlow.GetTracker("topic")`) with automatic fallback creation and thread-safe caching.
  - Indexer syntax (`_metricFlow["topic"]`), existence check (`TryGetTracker`), `DefaultTracker`, and `Trackers` collection enumeration.
- **Fluent Builder (`IMetricFlowBuilder`)**:
  - Implements `IFluentBuilder<IServiceCollection>` enabling clean, chained registration of multiple topic trackers via `AddMetricTracker(topic, configure)`.
  - Transparent `IServiceCollection` delegation allowing continuous chaining with any standard DI service registration.
- **Keyed Services & Dedicated Tracker Registration**:
  - Direct keyed dependency injection support in .NET 8+ via `[FromKeyedServices(topic)] IMetricTracker`.
  - Standalone `services.AddMetricFlowTracker(topic, configure)` extension for modular architectures.
- **Tag Enrichment Helpers (`AddTagsEnricher` & `AddHttpTagsEnricher`)**:
  - Fluent helper `options.AddTagsEnricher((tags) => ...)` on `MetricFlowOptions` for configuring topic-level tags across console, daemon, and worker services.
  - Fluent helper `options.AddHttpTagsEnricher((tags, context) => ...)` on `MetricFlowAspNetCoreOptions` with delegate chaining support for multiple HTTP request enrichers.
- **Advanced Console with DI Example (`AdvancedConsoleWithDIExample`)**:
  - Added new console example project demonstrating standard Microsoft DI integration, fluent multi-topic registration, `AddTagsEnricher`, keyed service resolution (`[FromKeyedServices]`), worker pipelines, and top-level `IMetricFlow` telemetry queries.

---

## [1.0.4] - 2026-09-20

### Added

- **Dimension & Breakdown Counter (`DimensionCounter` / `TagBreakdownCounter`)**:
  - Built-in dimensional counter that aggregates operation counts categorized by single tags, composite multi-tag combinations (`params string[] dimensionKeys`), or numeric metadata keys (e.g. `country`, `status`, `tenant_id`).
  - **Computed Dimension Selectors (`AddDimensionCounter`)**: Custom lambda support (`Func<tags, metadata, string?>`) enabling dynamic business classification rules and conditional counter filters without defining custom metric classes.
  - Native cardinality protection with configurable `maxUniqueValues` (defaults to 250) and automatic overflow rollup into `[Other]` to prevent memory leaks from high-cardinality tags.
  - Tracking of tracked/tagged vs. untracked/untagged operations, failed operations, and percentage distributions.
  - `DimensionSnapshot` (aliased as `TagBreakdownSnapshot`) implementing `IMetricSnapshot` with formatted output showing total operations, tracked percentages, and dimension distributions sorted descending by volume.
  - Fluent registration and querying extensions on `IMetricTracker`: `AddDimensionCounter(...)` and `GetDimensionValues(...)` (with `AddTagBreakdownCounter` and `AddComputedBreakdownCounter` aliases).
  - Fluent configuration on `MetricFlowOptions`: `options.AddDimensionCounter(...)`, `options.AddThroughputCounter(...)`, and `options.AddMemoryCounter(...)`.
- **Advanced Console Example**:
  - Added multi-counter regional dimension breakdown demonstration (`region`: US, EU, APAC) and summary output to `AdvancedConsoleExample`.
- **Automated Tests**:
  - Comprehensive unit and concurrency tests in `DimensionCounterTests` validating dimension matching, multi-tag combinations, dynamic scope tags, computed business selectors, conditional filters, cardinality overflow protection, and multi-threaded tracking.

---

## [1.0.3] - 2026-09-19

### Added

- **Throughput & Item Counter (`ThroughputCounter` / `ItemCounter`)**:
  - Built-in counter tracking processed item count, batch operations, and calculating processing throughput (`items/sec`).
  - Native tag recognition (`"items"`, `"count"`, `"batch_size"`) with case-insensitive parsing and default to 1 item/operation.
  - `ThroughputSnapshot` providing rich formatted output matching production telemetry (`TotalItems`, `ItemsPerSecond`, `AverageItemsPerOperation`, etc.).
  - `TrackItems` extension methods for upfront item count tracking on `IMetricTracker`.
  - `scope.SetItems()` and `scope.SetItemCount()` methods on tracking scopes for dynamic batch sizing during or upon completion of an operation.
  - Builder and snapshot querying extensions: `AddThroughputCounter()`, `AddItemCounter()`, and `GetThroughputSnapshot()` on `IMetricTracker`.
- **Dependency Injection for Core Applications (`DotnetKit.MetricFlow`)**:
  - `services.AddMetricFlow(topic, configure)` extension method for registering `MetricTracker`, `IMetricTracker`, and `IMetricSnapshotsSource` in standard Microsoft DI containers (`IServiceCollection`) without requiring ASP.NET Core dependencies.
  - Extracted shared `MetricFlowOptions` base configuration model.
- **Technical Metadata Telemetry (`Metadata`)**:
  - Added `Dictionary<string, long>? metadata` across `InContext`, `OutContext`, `CodeTracker`, and `IMetricTracker` (`Track`, `In`, `Out`).
  - Added `scope.SetMetadata(key, value)` extension method for non-string technical measurements.
  - High-performance numeric metadata ingestion for batch sizes, avoiding string conversions and allocations.
- **Console Examples**:
  - Added `BasicConsoleExample`: Clean, minimal zero-config starter demonstrating default `DurationCounter` tracking and `TrackAction`.
  - Added `AdvancedConsoleExample`: Multi-counter telemetry pipeline demonstrating duration, throughput (velocity & items/sec), memory, exception handling, and dynamic batch ingestion.

### Changed

- Extracted counter registration and snapshot querying methods from `MetricTracker` into reusable `TrackerCounterExtensions`.
- Extended `OutContext` constructor to support `metadata` and `utcTimestamp`.
- Updated `README.md` with throughput tracking, batch measurement, dependency injection, and updated console runners.

---

## [1.0.2] - 2026-09-17

### Added

- **ASP.NET Core Integration (`DotnetKit.MetricFlow.AspNetCore`)**:
  - Middleware (`MetricFlowMiddleware`) and endpoint routing integration for automated request duration, memory, and failure tracking.
  - Dependency injection extensions (`AddMetricFlow`, `UseMetricFlow`) with options support.
- **Delegate Tracking Extensions (`TrackerActionExtensions`)**:
  - `TrackAction` and `TrackActionAsync` overloads supporting synchronous actions, async tasks, and returning values with automatic exception and duration tracking.
- **Caller Member Name Resolution**:
  - Automatic metric name derivation via `[CallerMemberName]` across `Track()`, `TrackAction()`, and `TrackActionAsync()`.
- **Pluggable Counter Architecture**:
  - `DurationCounter`: Lock-free execution timing using high-precision stopwatch ticks and atomic aggregations.
  - `MemoryCounter`: Async-safe heap allocation tracking using thread-allocated bytes and process-wide delta fallback.
  - `ExceptionCounter`: Thread-safe failure tracking and exception type categorization.
  - `ICounter<TState>` and state-token lifecycle pattern (`OnIn` / `OnOut`).
  - Dynamic counter reconfiguration via `ICounterConfigObservable`.
- **Metric Snapshots**:
  - `IMetricSnapshotsSource` abstraction and formatting extensions grouping metrics cleanly by operation.
- **Multi-Targeting**:
  - Added support for `.NET 10.0` (`net10.0`) alongside `.NET 8.0` (`net8.0`).
- **Documentation**:
  - Added [ARCHITECTURE.md](ARCHITECTURE.md) documenting core design patterns, lock-free concurrency, and counter lifecycle.
  - Created a simple [ROADMAP.md](ROADMAP.md) focused on OpenTelemetry and cloud provider integrations.
- **CI/CD**:
  - MinVer automated versioning and OIDC-based NuGet publishing workflow.

### Changed

- Consolidated core namespaces into `DotnetKit.MetricFlow` and converted all files to file-scoped namespaces.
- Replaced legacy `StopWatchCounter` with the pluggable `DurationCounter` and atomic counter architecture.
- Modernized examples (`SimpleMetricCountersExample` and `WebApiExample`) to demonstrate new delegate tracking, memory counters, and caller member name capabilities.
- Simplified `README.md` with concise tracker capability examples.

---

## [1.0.1] - 2026-09-05

### Fixed

- **Duration Unit Bug (10,000× Inflation)**: `StopWatchCounter.Stop()` returned raw Stopwatch ticks which were incorrectly passed into `TimeSpan.FromMilliseconds`, inflating reported durations by ~10,000× (e.g. 2ms was reported as ~30,000ms). Now tracked using `TimeSpan.Ticks` and `Stopwatch.GetElapsedTime(...)`.
- **Thread Safety & Race Conditions**: Removed the shared mutable `Stopwatch` instance from `StopWatchCounter`. Timers are now measured per-operation via `Stopwatch.GetTimestamp()` in `CodeTracker`.
- **Concurrent `In`/`Out` Tracking**: Added `AsyncLocal` context stack in `MetricTrackerBase` so concurrent asynchronous executions (e.g. ASP.NET Core requests) accurately correlate their own start/end timings without clobbering each other.
- **Sampling Logic Overhaul**:
  - Fixed bug where `samplingRate: null` dropped 100% of metrics; null now correctly defaults to 100% collection.
  - Enforced sampling parity between `In` and `Out` so an operation dropped on `In` is guaranteed to be dropped on `Out` (preventing `InCount` and `OutCount` mismatch).
  - Replaced thread-unsafe `new Random()` with thread-safe `Random.Shared`.
- **Accurate Average Duration**: Replaced the flawed binary average `(initial + duration) / 2` with true cumulative arithmetic mean: `totalDurationTicks / outCount`.
- **Failing Unit Test**: Corrected `MetricTracker_ShouldTrackFailedMetrics` assertion where `In` was called twice but asserted 1.
- **Example Code Compilation**: Fixed `UtcCounter.cs` and `UtcCounters.cs` to inherit from `MetricTrackerBase<UtcCounter>` and `CounterBase` with valid constructors.

### Added

- `Dec(TimeSpan duration, bool? failed = false)` overload in `ICounter` and `CounterBase` to record precise duration directly.
- Optional `TimeSpan? duration` parameter in `IMetricTracker.Out` and `MetricTrackerBase.Out`.
- Automated tests in `MetricFlow.Tests`:
  - `MetricTracker_ShouldRecordAccurateDuration` (validates `Task.Delay(50)` records ~50ms).
  - `MetricTracker_ShouldHandleConcurrentTracking` (validates 20 parallel tasks under high concurrency).
  - `MetricTracker_SamplingRateNull_ShouldTrackAll` (validates `null` sampling rate tracks all events).
- PR validation GitHub Actions workflow (`.github/workflows/pr-validation.yml`).
- `ROADMAP.md` tracking phased improvements and project vision.

### Changed

- Cleaned up unreferenced empty folders `src/DotnetKit.MetricFlow.Core` and `src/DotnetKit.MetricFlow.Collector`.
- Upgraded GitHub Actions deployment workflows from `@v1`/`@v2` to `@v4`.
- Updated `SimpleMetricCountersExample/BenchRunner.cs` comments with accurate millisecond durations.

---

## [1.0.0] - 2024-05-15

### Added

- Core `MetricTracker` and `MetricTrackerBase<T>` generic abstraction.
- `StopWatchCounter` and `CounterBase` counter implementations.
- `CodeTracker<T>` disposable pattern (`using (tracker.Track(...))`).
- Topic-level tags (`TopicTags`) and metric-level metadata (`Metadata`).
- Sampling rate support (`samplingRate`).
- Failed metric tracking (`Out(..., failed: true)`).
- `CounterValues` record for formatted metric summaries (counts, min, max, avg, total).
- `SimpleMetricCountersExample` console runner.
- `WebApiExample` ASP.NET Core demonstration.
- GitHub Actions CI/CD workflows for NuGet deployment.
