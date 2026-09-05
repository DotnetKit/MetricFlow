# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned (Phase 2 & 3)
- Zero-allocation `MetricScope` struct for `using (tracker.Track(...))`.
- `DotnetKit.MetricFlow.AspNetCore` package with turnkey middleware and endpoint routing filters.
- Histogram & latency percentiles (P50, P75, P90, P99, P99.9).
- OpenTelemetry integration bridging to `System.Diagnostics.Metrics`.
- Native Prometheus text exposition endpoint (`/metrics`).
- Background metric collector and pusher (`DotnetKit.MetricFlow.Collector`).

---

## [1.1.0] - 2026-09-05

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
