# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.3] - 2026-09-19

### Added

- **Throughput & Item Counter (`ThroughputCounter` / `ItemCounter`)**:
  - Built-in counter tracking processed item count, batch operations, and calculating processing throughput (`items/sec`).
  - Native tag recognition (`"items"`, `"count"`, `"batch_size"`) with case-insensitive parsing and default to 1 item/operation.
  - `ThroughputSnapshot` providing rich formatted output matching production telemetry (`TotalItems`, `ItemsPerSecond`, `AverageItemsPerOperation`, etc.).
  - `TrackItems` extension methods for upfront item count tracking on `IMetricTracker`.
  - `scope.SetItems()` and `scope.SetItemCount()` methods on tracking scopes for dynamic batch sizing during or upon completion of an operation.
  - Builder and snapshot querying extensions: `AddThroughputCounter()`, `AddItemCounter()`, and `GetThroughputValues()` on `IMetricTracker`.
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
