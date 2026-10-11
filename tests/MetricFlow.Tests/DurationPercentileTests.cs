using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MetricFlow.Tests;

public class DurationPercentileTests
{
    [Fact]
    public void QuantileReservoir_WithKnownValues_CalculatesExpectedPercentiles()
    {
        // Arrange: 100 samples from 1ms to 100ms
        var reservoir = new QuantileReservoir(capacity: 200);
        for (int i = 1; i <= 100; i++)
        {
            reservoir.Record(TimeSpan.FromMilliseconds(i).Ticks);
        }

        // Act
        var percentiles = reservoir.GetPercentiles([0.50, 0.90, 0.95, 0.99]);

        // Assert
        percentiles[0.50].TotalMilliseconds.Should().BeApproximately(50.5, 0.5);
        percentiles[0.90].TotalMilliseconds.Should().BeApproximately(90.1, 0.5);
        percentiles[0.95].TotalMilliseconds.Should().BeApproximately(95.05, 0.5);
        percentiles[0.99].TotalMilliseconds.Should().BeApproximately(99.01, 0.5);
    }

    [Fact]
    public void QuantileReservoir_ExceedingCapacity_MaintainsBoundedSlidingWindow()
    {
        // Arrange: capacity 10, write 20 items (11..20 overwrite 1..10)
        var reservoir = new QuantileReservoir(capacity: 10);
        for (int i = 1; i <= 20; i++)
        {
            reservoir.Record(TimeSpan.FromMilliseconds(i).Ticks);
        }

        // Act
        var percentiles = reservoir.GetPercentiles([0.0, 0.50, 1.0]);

        // Assert: reservoir should contain 11..20
        reservoir.TotalCount.Should().Be(20);
        percentiles[0.0].TotalMilliseconds.Should().Be(11);
        percentiles[0.50].TotalMilliseconds.Should().BeApproximately(15.5, 0.5);
        percentiles[1.0].TotalMilliseconds.Should().Be(20);
    }

    [Fact]
    public void QuantileReservoir_ConcurrentWrites_IsThreadSafe()
    {
        // Arrange
        var reservoir = new QuantileReservoir(capacity: 1024);

        // Act
        Parallel.For(0, 10_000, i =>
        {
            reservoir.Record(TimeSpan.FromMilliseconds((i % 100) + 1).Ticks);
        });

        // Assert
        reservoir.TotalCount.Should().Be(10_000);
        var p = reservoir.GetPercentiles([0.50, 0.95]);
        p[0.50].TotalMilliseconds.Should().BeInRange(40, 60);
        p[0.95].TotalMilliseconds.Should().BeInRange(90, 100);
    }

    [Fact]
    public void DurationCounter_DefaultOptions_PopulatesPercentilesInSnapshot()
    {
        // Arrange
        var counter = new DurationCounter();
        for (int i = 1; i <= 100; i++)
        {
            var ctx = new DotnetKit.MetricFlow.Abstractions.OutContext(
                metricName: "SearchQuery",
                failed: false,
                exception: null,
                duration: TimeSpan.FromMilliseconds(i),
                tags: null,
                metadata: null);

            counter.OnOut(null, in ctx);
        }

        // Act
        var snapshot = counter.GetSnapshot("SearchQuery") as DurationSnapshot;

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.P50Duration.Should().NotBeNull();
        snapshot.P95Duration.Should().NotBeNull();
        snapshot.P99Duration.Should().NotBeNull();
        snapshot.P50Duration!.Value.TotalMilliseconds.Should().BeApproximately(50.5, 0.5);
        snapshot.P95Duration!.Value.TotalMilliseconds.Should().BeApproximately(95.05, 0.5);
        snapshot.P99Duration!.Value.TotalMilliseconds.Should().BeApproximately(99.01, 0.5);
        snapshot.P50.Should().Be(snapshot.P50Duration);
        snapshot.P95.Should().Be(snapshot.P95Duration);
        snapshot.P99.Should().Be(snapshot.P99Duration);
        snapshot.Percentiles.Should().NotBeNull();
    }

    [Fact]
    public void DurationCounter_WhenPercentilesDisabled_LeavesPercentilesNull()
    {
        // Arrange
        var counter = new DurationCounter(new DurationCounterOptions { EnablePercentiles = false });
        var ctx = new DotnetKit.MetricFlow.Abstractions.OutContext(
            metricName: "QuickOp",
            failed: false,
            exception: null,
            duration: TimeSpan.FromMilliseconds(10),
            tags: null,
            metadata: null);

        counter.OnOut(null, in ctx);

        // Act
        var snapshot = counter.GetSnapshot("QuickOp") as DurationSnapshot;

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.P50Duration.Should().BeNull();
        snapshot.P95Duration.Should().BeNull();
        snapshot.Percentiles.Should().BeNull();
    }

    [Fact]
    public void ConsoleMetricSink_WithPercentiles_RendersPercentileBreakdown()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var sink = new ConsoleMetricSink(new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = false,
            IncludeTimestamp = false,
            Prefix = "[TestPrefix]"
        });

        var snapshot = new DurationSnapshot(
            MetricName: "Checkout",
            CounterName: "Duration",
            InCount: 10,
            OutCount: 10,
            FailedCount: 0,
            TotalDuration: TimeSpan.FromMilliseconds(500),
            AverageDuration: TimeSpan.FromMilliseconds(50),
            MinDuration: TimeSpan.FromMilliseconds(10),
            MaxDuration: TimeSpan.FromMilliseconds(120),
            Timestamp: DateTime.UtcNow,
            P50Duration: TimeSpan.FromMilliseconds(45),
            P90Duration: TimeSpan.FromMilliseconds(85),
            P95Duration: TimeSpan.FromMilliseconds(100),
            P99Duration: TimeSpan.FromMilliseconds(115)
        );

        // Act
        sink.Emit([snapshot]);
        var output = stringWriter.ToString().Trim();

        // Assert
        output.Should().Contain("p50: 45.00 ms");
        output.Should().Contain("p95: 100.00 ms");
        output.Should().Contain("p99: 115.00 ms");
        output.Should().Contain("Avg: 50.00 ms");
    }

    [Fact]
    public void LoggerMetricSink_WithPercentiles_LogsStructuredPercentiles()
    {
        // Arrange
        var testLogger = new TestLogger();
        var sink = new LoggerMetricSink(testLogger, new LoggerMetricSinkOptions
        {
            IncludeTimestamp = false
        });

        var snapshot = new DurationSnapshot(
            MetricName: "Payment",
            CounterName: "Duration",
            InCount: 1,
            OutCount: 1,
            FailedCount: 0,
            TotalDuration: TimeSpan.FromMilliseconds(30),
            AverageDuration: TimeSpan.FromMilliseconds(30),
            MinDuration: TimeSpan.FromMilliseconds(30),
            MaxDuration: TimeSpan.FromMilliseconds(30),
            Timestamp: DateTime.UtcNow,
            P50Duration: TimeSpan.FromMilliseconds(30),
            P90Duration: TimeSpan.FromMilliseconds(30),
            P95Duration: TimeSpan.FromMilliseconds(30),
            P99Duration: TimeSpan.FromMilliseconds(30)
        );

        // Act
        sink.Emit([snapshot]);

        // Assert
        testLogger.Logs.Should().HaveCount(1);
        var message = testLogger.Logs[0].Message;
        message.Should().Contain("p50: 30.00ms");
        message.Should().Contain("p95: 30.00ms");
    }

    [Fact]
    public void DependencyInjection_ConfigureDuration_ConfiguresCustomReservoir()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetricFlow("TestTopic", opt =>
        {
            opt.ConfigureDuration(dur =>
            {
                dur.ReservoirSize = 512;
                dur.Percentiles = [0.50, 0.99];
            });
        });

        using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<MetricTracker>();

        // Act
        using (tracker.Track("DatabaseQuery"))
        {
            Thread.Sleep(5);
        }

        var snapshot = tracker.GetDurationSnapshot("DatabaseQuery");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.P50Duration.Should().NotBeNull();
        snapshot.P99Duration.Should().NotBeNull();
        tracker.DurationCounter.Options.ReservoirSize.Should().Be(512);
    }

    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId, string Message, Exception? Exception)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Logs.Add((logLevel, eventId, message, exception));
        }
    }
}
