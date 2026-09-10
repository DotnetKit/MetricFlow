using DotnetKit.MetricFlow.Tracker;
using DotnetKit.MetricFlow.Tracker.Configuration;
using DotnetKit.MetricFlow.Tracker.Counters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests
{
    public class PatternACustomCountersTests
    {
        [Fact]
        public async Task CompositeTracker_ShouldTrackDurationMemoryAndExceptionsSimultaneously()
        {
            // Arrange
            var tracker = new MetricTracker("CompositeTest")
                .AddMemoryCounter()
                .AddExceptionCounter();

            // Act - 2 successful operations, 1 failed operation
            using (tracker.Track("MultiMetricOp"))
            {
                var dummyBuffer = new byte[1024 * 50]; // Allocate 50 KB
                await Task.Delay(5);
                _ = dummyBuffer.Length;
            }

            using (tracker.Track("MultiMetricOp"))
            {
                var dummyBuffer = new byte[1024 * 20]; // Allocate 20 KB
                await Task.Delay(5);
                _ = dummyBuffer.Length;
            }

            try
            {
                using (var scope = (CodeTracker)tracker.Track("MultiMetricOp"))
                {
                    try
                    {
                        throw new InvalidOperationException("Simulated error");
                    }
                    catch (Exception ex)
                    {
                        scope.SetException(ex);
                        throw;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert
            var durationSnapshot = tracker.GetSnapshot("MultiMetricOp", DurationCounter.DefaultCounterName) as DurationSnapshot;
            durationSnapshot.Should().NotBeNull();
            durationSnapshot!.InCount.Should().Be(3);
            durationSnapshot.OutCount.Should().Be(3);
            durationSnapshot.FailedCount.Should().Be(1);
            durationSnapshot.TotalDuration.TotalMilliseconds.Should().BeGreaterThan(0);

            var memorySnapshot = tracker.GetSnapshot("MultiMetricOp", MemoryCounter.DefaultCounterName) as MemorySnapshot;
            memorySnapshot.Should().NotBeNull();
            memorySnapshot!.OperationCount.Should().Be(3);
            memorySnapshot.TotalAllocatedBytes.Should().BeGreaterThan(1024 * 50);

            var exceptionSnapshot = tracker.GetSnapshot("MultiMetricOp", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
            exceptionSnapshot.Should().NotBeNull();
            exceptionSnapshot!.TotalOperations.Should().Be(3);
            exceptionSnapshot.TotalFailures.Should().Be(1);
            exceptionSnapshot.ExceptionsByType.Should().ContainKey(nameof(InvalidOperationException));
            exceptionSnapshot.ExceptionsByType[nameof(InvalidOperationException)].Should().Be(1);
        }

        [Fact]
        public void RealtimeConfigWatch_ShouldDynamicallyDisableAndEnableCounters()
        {
            // Arrange
            var configNotifier = new CounterConfigNotifier();
            var tracker = new MetricTracker("DynamicConfigTopic", configObservable: configNotifier)
                .AddExceptionCounter();

            // Act 1: Initial state (both enabled)
            tracker.In("DynamicMetric");
            tracker.Out("DynamicMetric", failed: true);

            var exSnapshot1 = tracker.GetSnapshot("DynamicMetric", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
            exSnapshot1.Should().NotBeNull();
            exSnapshot1!.TotalFailures.Should().Be(1);

            // Act 2: Dynamically disable ExceptionCounter via observable notification
            configNotifier.NotifyChanged(ExceptionCounter.DefaultCounterName, enabled: false);

            tracker.In("DynamicMetric");
            tracker.Out("DynamicMetric", failed: true);

            var exSnapshot2 = tracker.GetSnapshot("DynamicMetric", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
            exSnapshot2!.TotalFailures.Should().Be(1); // Should not increase because counter is disabled

            // DurationCounter is still enabled and should track
            var durationSnapshot = tracker.GetValues("DynamicMetric");
            durationSnapshot.Should().NotBeNull();
            durationSnapshot!.InCount.Should().Be(2);

            // Act 3: Dynamically re-enable ExceptionCounter
            configNotifier.NotifyChanged(ExceptionCounter.DefaultCounterName, enabled: true);

            tracker.In("DynamicMetric");
            tracker.Out("DynamicMetric", failed: true);

            var exSnapshot3 = tracker.GetSnapshot("DynamicMetric", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
            exSnapshot3!.TotalFailures.Should().Be(2); // Resumed tracking!
        }

        [Fact]
        public async Task MultiCounter_ConcurrentTracking_ShouldBeThreadSafe()
        {
            // Arrange
            var tracker = new MetricTracker("ConcurrentTopic")
                .AddMemoryCounter()
                .AddExceptionCounter();

            const int concurrency = 30;

            // Act
            var tasks = Enumerable.Range(0, concurrency).Select(async i =>
            {
                using (var scope = (CodeTracker)tracker.Track("ConcurrentMetric"))
                {
                    var data = new byte[1024];
                    await Task.Delay(5);
                    _ = data.Length;

                    if (i % 3 == 0)
                    {
                        scope.SetException(new HttpRequestException($"Error {i}"));
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Assert
            var duration = tracker.GetValues("ConcurrentMetric");
            duration.Should().NotBeNull();
            duration!.InCount.Should().Be(concurrency);
            duration.OutCount.Should().Be(concurrency);
            duration.FailedCount.Should().Be(10); // 30 / 3

            var exceptions = tracker.GetSnapshot("ConcurrentMetric", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
            exceptions.Should().NotBeNull();
            exceptions!.TotalFailures.Should().Be(10);
            exceptions.ExceptionsByType[nameof(HttpRequestException)].Should().Be(10);

            var memory = tracker.GetSnapshot("ConcurrentMetric", MemoryCounter.DefaultCounterName) as MemorySnapshot;
            memory.Should().NotBeNull();
            memory!.OperationCount.Should().Be(concurrency);
            memory.TotalAllocatedBytes.Should().BeGreaterThan(1024 * concurrency);
        }

        [Fact]
        public void FormattedSnapshots_ShouldContainUsefulDiagnosticStrings()
        {
            // Arrange
            var tracker = new MetricTracker("DiagnosticsTopic")
                .AddMemoryCounter()
                .AddExceptionCounter();

            tracker.In("DiagOp");
            tracker.Out("DiagOp", exception: new TimeoutException("Operation timed out"));

            // Act
            var output = tracker.ToString();

            // Assert
            output.Should().Contain("DiagnosticsTopic");
            output.Should().Contain("Duration");
            output.Should().Contain("Memory");
            output.Should().Contain("Exception");
            output.Should().Contain("TimeoutException");
        }
    }
}
