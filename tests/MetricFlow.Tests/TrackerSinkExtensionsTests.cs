using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MetricFlow.Tests;

public class TrackerSinkExtensionsTests
{
    [Fact]
    public void AddConsoleSink_OnStandaloneMetricTracker_RegistersConsoleSink()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var tracker = new MetricTracker("OrderProcessing")
            .AddThroughputCounter()
            .AddConsoleSink(opt =>
            {
                opt.OutputWriter = stringWriter;
                opt.Prefix = "[StandaloneOrder]";
                opt.Colorize = false;
            });

        // Act
        tracker.TrackItems("Checkout", 10).Dispose();
        tracker.FlushSinks();

        // Assert
        tracker.GetSinks().Should().ContainSingle(s => s is ConsoleMetricSink);
        var output = stringWriter.ToString();
        output.Should().Contain("[StandaloneOrder]");
        output.Should().Contain("Checkout");
    }

    [Fact]
    public void AddLoggerSink_WithDirectILogger_RegistersLoggerSink()
    {
        // Arrange
        var testLogger = new TestLogger();
        var tracker = new MetricTracker("PaymentService")
            .AddThroughputCounter()
            .AddLoggerSink(
                testLogger,
                opt =>
                {
                    opt.Prefix = "[Payments]";
                    opt.LogLevel = LogLevel.Information;
                }
            );

        // Act
        tracker.TrackItems("Authorize", 5).Dispose();
        tracker.FlushSinks();

        // Assert
        tracker.GetSinks().Should().ContainSingle(s => s is LoggerMetricSink);
        testLogger.Logs.Should().NotBeEmpty();
        testLogger
            .Logs.Should()
            .Contain(l => l.Message.Contains("[Payments]") && l.Message.Contains("Authorize"));
    }

    [Fact]
    public void AddLoggerSink_WithOptionsOnly_RegistersLoggerSink()
    {
        // Arrange
        var testLogger = new TestLogger();
        var tracker = new MetricTracker("InventoryService")
            .AddThroughputCounter()
            .AddLoggerSink(opt =>
            {
                opt.Logger = testLogger;
                opt.Prefix = "[Inventory]";
            });

        // Act
        tracker.TrackItems("ReserveItem", 2).Dispose();
        tracker.FlushSinks();

        // Assert
        tracker.GetSinks().Should().ContainSingle(s => s is LoggerMetricSink);
        testLogger
            .Logs.Should()
            .Contain(l => l.Message.Contains("[Inventory]") && l.Message.Contains("ReserveItem"));
    }

    [Fact]
    public void AddObservableSink_OnTracker_RegistersAndEmitsSnapshots()
    {
        // Arrange
        var emittedSnapshots = new List<IReadOnlyList<IMetricSnapshot>>();
        var tracker = new MetricTracker("StreamProcessing")
            .AddThroughputCounter()
            .AddObservableSink(out var observable);

        using var subscription = observable.Subscribe(emittedSnapshots.Add);

        // Act
        tracker.TrackItems("ProcessBatch", 50).Dispose();
        tracker.FlushSinks();

        // Assert
        emittedSnapshots.Should().NotBeEmpty();
        emittedSnapshots.SelectMany(b => b).Should().Contain(s => s.MetricName == "ProcessBatch");
    }

    [Fact]
    public void ConfigureSinkTriggers_EnablesExecutionStrideSampling()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var tracker = new MetricTracker("BatchService")
            .AddThroughputCounter()
            .AddConsoleSink(opt =>
            {
                opt.OutputWriter = stringWriter;
                opt.Colorize = false;
            })
            .ConfigureSinkTriggers(trig =>
            {
                trig.EmitEveryNExecutions = 3;
            });

        // Act - 1st & 2nd run should NOT trigger sink emission
        tracker.TrackItems("Step", 1).Dispose();
        tracker.TrackItems("Step", 1).Dispose();
        stringWriter.ToString().Should().BeEmpty();

        // 3rd run should trigger automatically
        tracker.TrackItems("Step", 1).Dispose();

        // Assert
        stringWriter.ToString().Should().NotBeEmpty();
        stringWriter.ToString().Should().Contain("Step");
    }

    [Fact]
    public async Task EnablePeriodicFlush_FlushesSinksOnInterval()
    {
        // Arrange
        var testLogger = new TestLogger();
        var tracker = new MetricTracker("PeriodicService")
            .AddThroughputCounter()
            .AddLoggerSink(testLogger);

        // Act - enable periodic flush every 30ms
        using (var flushToken = tracker.EnablePeriodicFlush(TimeSpan.FromMilliseconds(30)))
        {
            tracker.TrackItems("JobA", 100).Dispose();

            // Wait a little for timer to fire at least once
            await Task.Delay(100);

            testLogger.Logs.Should().NotBeEmpty();
            testLogger.Logs.Should().Contain(l => l.Message.Contains("JobA"));
        }
    }

    [Fact]
    public void EnablePeriodicFlush_FlushesFinalMetricsOnDispose()
    {
        // Arrange
        var testLogger = new TestLogger();
        var tracker = new MetricTracker("DisposeFlushService")
            .AddThroughputCounter()
            .AddLoggerSink(testLogger);

        // Act - configure 1 hour interval so timer won't fire during test
        using (var flushToken = tracker.EnablePeriodicFlush(TimeSpan.FromHours(1)))
        {
            tracker.TrackItems("ShutdownJob", 42).Dispose();
            testLogger.Logs.Should().BeEmpty(); // Has not flushed yet
        } // Disposing flushToken must trigger final flush

        // Assert
        testLogger.Logs.Should().NotBeEmpty();
        testLogger.Logs.Should().Contain(l => l.Message.Contains("ShutdownJob"));
    }

    [Fact]
    public void TopicLevelAggregateQueries_CalculatesCumulativeMemoryAndItems()
    {
        // Arrange
        var tracker = new MetricTracker("MultiStagePipeline")
            .AddThroughputCounter()
            .AddMemoryCounter()
            .AddFailureCounter();

        // Stage 1
        tracker.TrackItems("Stage1_Extract", 100).Dispose();
        // Stage 2
        tracker.TrackItems("Stage2_Transform", 250).Dispose();
        // Stage 3
        tracker.TrackItems("Stage3_Load", 150).Dispose();

        // Act
        var totalItems = tracker.GetTotalItemsProcessed();
        var totalItemsAlias = tracker.GetTotalItems();
        var totalMemory = tracker.GetTotalMemoryAllocated();
        var failureSnapshot = tracker.GetFailureSnapshot("Stage1_Extract");

        // Assert
        totalItems.Should().Be(500);
        totalItemsAlias.Should().Be(500);
        totalMemory.Should().BeGreaterThanOrEqualTo(0);
        failureSnapshot.Should().NotBeNull();
        failureSnapshot!.TotalOperations.Should().Be(1);
    }

    private sealed class TestLogger : ILogger
    {
        public List<(
            LogLevel Level,
            EventId EventId,
            string Message,
            Exception? Exception
        )> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var message = formatter(state, exception);
            Logs.Add((logLevel, eventId, message, exception));
        }
    }
}
