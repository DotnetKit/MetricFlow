using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MetricFlow.Tests;

public class MetricSinkTests
{
    [Fact]
    public void ConsoleMetricSink_WithPlainFormatting_FormatsExpectedOutput()
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

        var tpSnapshot = new ThroughputSnapshot(
            MetricName: "ProcessOrder",
            CounterName: "Throughput",
            TotalItems: 250,
            TotalOperations: 5,
            TotalDuration: TimeSpan.FromSeconds(2),
            ItemsPerSecond: 125.0,
            AverageItemsPerOperation: 50.0,
            Timestamp: DateTime.UtcNow,
            FailedOperations: 1);

        // Act
        sink.Emit(new[] { tpSnapshot });
        var output = stringWriter.ToString().Trim();

        // Assert
        output.Should().Contain("[TestPrefix]");
        output.Should().Contain("[Throughput:ProcessOrder]");
        output.Should().Contain("TotalItems: 250");
        output.Should().Contain("Operations: 5");
        output.Should().Contain("Rate: 125.0 items/s");
        output.Should().Contain("Failed: 1");
        output.Should().NotContain("\u001b"); // No ANSI escape codes
    }

    [Fact]
    public void ConsoleMetricSink_WithColorize_IncludesAnsiEscapes()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var sink = new ConsoleMetricSink(new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = true,
            IncludeTimestamp = true,
            Prefix = "[MetricFlow]"
        });

        var failureSnapshot = new FailureSnapshot(
            MetricName: "Checkout",
            CounterName: "Failure",
            TotalOperations: 10,
            TotalFailures: 2,
            LogicalFailures: 1,
            ExceptionFailures: 1,
            Timestamp: DateTime.UtcNow);

        // Act
        sink.Emit(new[] { failureSnapshot });
        var output = stringWriter.ToString();

        // Assert
        output.Should().Contain("\u001b["); // ANSI escape sequence
        output.Should().Contain("Checkout");
        output.Should().Contain("Failed: \u001b[31m2 (20.0%)");
    }

    [Fact]
    public void ConsoleMetricSink_WithDurationThresholds_AppliesExpectedAnsiColors()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var options = new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = true,
            IncludeTimestamp = false,
            Prefix = "[Test]"
        };

        options.AddThreshold<DurationSnapshot>(
            warn: TimeSpan.FromMilliseconds(700),
            critical: TimeSpan.FromMilliseconds(2000)
        );

        var sink = new ConsoleMetricSink(options);

        var normalSnapshot = new DurationSnapshot("FastOp", "Duration", 1, 1, 0,
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200), DateTime.UtcNow);

        var warnSnapshot = new DurationSnapshot("SlowOp", "Duration", 1, 1, 0,
            TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(1200),
            TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(1200), DateTime.UtcNow);

        var critSnapshot = new DurationSnapshot("CriticalOp", "Duration", 1, 1, 0,
            TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(2500),
            TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(2500), DateTime.UtcNow);

        // Act
        var normalLine = sink.FormatSnapshot(normalSnapshot);
        var warnLine = sink.FormatSnapshot(warnSnapshot);
        var critLine = sink.FormatSnapshot(critSnapshot);

        // Assert - Normal (< 700ms) uses Green (\u001b[32m)
        normalLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.Green));
        normalLine.Should().Contain("200.00 ms");

        // Assert - Warning (>= 700ms and < 2000ms) uses DarkYellow (\u001b[33m)
        warnLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.DarkYellow));
        warnLine.Should().Contain("1200.00 ms");

        // Assert - Critical (>= 2000ms) uses Red (\u001b[31m)
        critLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.Red));
        critLine.Should().Contain("2500.00 ms");
    }

    [Fact]
    public void ConsoleMetricSink_WithColorSelector_OverridesThresholdsAndAppliesColors()
    {
        // Arrange
        var options = new ConsoleMetricSinkOptions
        {
            Colorize = true,
            IncludeTimestamp = false
        };

        options.ColorSelector = snapshot => snapshot switch
        {
            DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 2000 => ConsoleColor.Red,
            DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 700 => ConsoleColor.DarkYellow,
            ExceptionSnapshot { TotalExceptions: > 0 } => ConsoleColor.Red,
            _ => ConsoleColor.Green
        };

        var sink = new ConsoleMetricSink(options);

        var durationCrit = new DurationSnapshot("Checkout", "Duration", 1, 1, 0,
            TimeSpan.FromMilliseconds(3000), TimeSpan.FromMilliseconds(3000),
            TimeSpan.FromMilliseconds(3000), TimeSpan.FromMilliseconds(3000), DateTime.UtcNow);

        var exceptionSnap = new ExceptionSnapshot("Payment", "Exception", 10, 2,
            new Dictionary<string, long> { ["InvalidOperationException"] = 2 }, DateTime.UtcNow);

        var otherSnap = new ThroughputSnapshot("Search", "Throughput", 100, 10,
            TimeSpan.FromSeconds(1), 100.0, 10.0, DateTime.UtcNow);

        // Act
        var critLine = sink.FormatSnapshot(durationCrit);
        var exLine = sink.FormatSnapshot(exceptionSnap);
        var otherLine = sink.FormatSnapshot(otherSnap);

        // Assert
        critLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.Red));
        exLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.Red));
        otherLine.Should().Contain(ConsoleMetricSink.ToAnsi(ConsoleColor.Green));
    }

    [Fact]
    public void ConsoleMetricSink_WhenColorizeFalse_StripsAllAnsiCodesEvenWithThresholds()
    {
        // Arrange
        var options = new ConsoleMetricSinkOptions
        {
            Colorize = false,
            IncludeTimestamp = false
        };

        options.AddThreshold<DurationSnapshot>(
            warn: TimeSpan.FromMilliseconds(500),
            critical: TimeSpan.FromMilliseconds(1000)
        );

        var sink = new ConsoleMetricSink(options);

        var snapshot = new DurationSnapshot("Job", "Duration", 1, 1, 0,
            TimeSpan.FromMilliseconds(1500), TimeSpan.FromMilliseconds(1500),
            TimeSpan.FromMilliseconds(1500), TimeSpan.FromMilliseconds(1500), DateTime.UtcNow);

        // Act
        var line = sink.FormatSnapshot(snapshot);

        // Assert
        line.Should().NotContain("\u001b");
        line.Should().Contain("1500.00 ms");
    }

    [Fact]
    public void ConsoleMetricSink_WithNumericAndInvertedThresholds_BehavesCorrectly()
    {
        // Arrange
        var options = new ConsoleMetricSinkOptions();

        // Inverted: lower throughput is critical
        options.AddThreshold<ThroughputSnapshot>(
            warn: 50.0,
            critical: 10.0,
            warnColor: ConsoleColor.Yellow,
            criticalColor: ConsoleColor.DarkRed,
            normalColor: ConsoleColor.Cyan
        );

        var highThroughput = new ThroughputSnapshot("Orders", "Throughput", 1000, 10, TimeSpan.FromSeconds(10), 100.0, 100.0, DateTime.UtcNow);
        var lowThroughput = new ThroughputSnapshot("Orders", "Throughput", 300, 10, TimeSpan.FromSeconds(10), 30.0, 30.0, DateTime.UtcNow);
        var critThroughput = new ThroughputSnapshot("Orders", "Throughput", 50, 10, TimeSpan.FromSeconds(10), 5.0, 5.0, DateTime.UtcNow);

        // Act & Assert
        options.ResolveColor(highThroughput).Should().Be(ConsoleColor.Cyan);
        options.ResolveColor(lowThroughput).Should().Be(ConsoleColor.Yellow);
        options.ResolveColor(critThroughput).Should().Be(ConsoleColor.DarkRed);

        // Clear thresholds
        options.ClearThresholds();
        options.ThresholdRules.Should().BeEmpty();
        options.ResolveColor(critThroughput).Should().BeNull();
    }

    [Fact]
    public void ObservableMetricSink_BroadcastsSnapshotsToSubscribers()
    {
        // Arrange
        using var observableSink = new ObservableMetricSink("RxSink");
        var received = new List<IReadOnlyList<IMetricSnapshot>>();

        using var subscription = observableSink.Subscribe(snapshots =>
        {
            received.Add(snapshots);
        });

        var snapshot1 = new ThroughputSnapshot("MetricA", "Throughput", 10, 1, TimeSpan.FromSeconds(1), 10, 10, DateTime.UtcNow);
        var snapshot2 = new ThroughputSnapshot("MetricB", "Throughput", 20, 2, TimeSpan.FromSeconds(1), 20, 10, DateTime.UtcNow);

        // Act
        observableSink.Emit(new[] { snapshot1 });
        observableSink.Emit(new[] { snapshot2 });

        // Assert
        received.Should().HaveCount(2);
        received[0][0].MetricName.Should().Be("MetricA");
        received[1][0].MetricName.Should().Be("MetricB");

        // Unsubscribe
        subscription.Dispose();
        observableSink.Emit(new[] { snapshot1 });

        received.Should().HaveCount(2); // No new events after unregistering
    }

    [Fact]
    public void MetricTracker_WithStrideTrigger_FlushesOnlyOnNthExecution()
    {
        // Arrange
        var testSink = new RecordingMetricSink();
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders",
            Sinks = { testSink },
            SinkTriggers = new MetricSinkTriggerOptions
            {
                EmitEveryNExecutions = 3
            }
        });

        // Act
        tracker.Track("Process").Dispose(); // 1
        testSink.EmittedBatches.Should().BeEmpty();

        tracker.Track("Process").Dispose(); // 2
        testSink.EmittedBatches.Should().BeEmpty();

        tracker.Track("Process").Dispose(); // 3 -> Trigger!
        testSink.EmittedBatches.Should().HaveCount(1);
        testSink.EmittedBatches[0].Should().Contain(s => s.MetricName == "Process");

        tracker.Track("Process").Dispose(); // 4
        tracker.Track("Process").Dispose(); // 5
        testSink.EmittedBatches.Should().HaveCount(1);

        tracker.Track("Process").Dispose(); // 6 -> Trigger!
        testSink.EmittedBatches.Should().HaveCount(2);
    }

    [Fact]
    public void MetricTracker_WithFailureTrigger_EmitsImmediatelyOnFailure()
    {
        // Arrange
        var testSink = new RecordingMetricSink();
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Billing",
            Sinks = { testSink },
            SinkTriggers = new MetricSinkTriggerOptions
            {
                EmitOnFailure = true
            }
        });

        // Act - Success
        tracker.In("Charge");
        tracker.Out("Charge", failed: false);
        testSink.EmittedBatches.Should().BeEmpty();

        // Act - Failure
        tracker.In("Charge");
        tracker.Out("Charge", failed: true, exception: new InvalidOperationException("Card declined"));

        // Assert
        testSink.EmittedBatches.Should().HaveCount(1);
        testSink.EmittedBatches[0].Should().Contain(s => s.MetricName == "Charge");
    }

    [Fact]
    public void MetricTracker_WithSlowDurationTrigger_EmitsWhenExceedingThreshold()
    {
        // Arrange
        var testSink = new RecordingMetricSink();
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Database",
            Sinks = { testSink },
            SinkTriggers = new MetricSinkTriggerOptions
            {
                EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(50)
            }
        });

        // Act - Fast query (10ms)
        tracker.In("Select");
        tracker.Out("Select", duration: TimeSpan.FromMilliseconds(10));
        testSink.EmittedBatches.Should().BeEmpty();

        // Act - Slow query (120ms)
        tracker.In("Select");
        tracker.Out("Select", duration: TimeSpan.FromMilliseconds(120));

        // Assert
        testSink.EmittedBatches.Should().HaveCount(1);
        testSink.EmittedBatches[0].Should().Contain(s => s.MetricName == "Select");
    }

    [Fact]
    public async Task MetricTracker_ManualFlush_EmitsAllSnapshotsToSinks()
    {
        // Arrange
        var testSink = new RecordingMetricSink();
        var tracker = new MetricTracker("Inventory");
        tracker.RegisterSink(testSink);

        using (tracker.Track("AddItem")) { }
        using (tracker.Track("RemoveItem")) { }

        testSink.EmittedBatches.Should().BeEmpty();

        // Act - Sync flush
        tracker.FlushSinks();
        testSink.EmittedBatches.Should().HaveCount(1);
        testSink.EmittedBatches[0].Select(s => s.MetricName).Distinct().Should().Contain(new[] { "AddItem", "RemoveItem" });

        // Act - Async flush
        await tracker.FlushSinksAsync();
        testSink.EmittedBatches.Should().HaveCount(2);
    }

    [Fact]
    public void MetricFlowOptions_FluentConfiguration_WiresSinksProperly()
    {
        // Arrange
        var options = new MetricFlowOptions();
        options.AddConsoleSink(opt =>
        {
            opt.Colorize = false;
            opt.Prefix = "[App]";
        });

        options.AddObservableSink(out var observable);
        options.ConfigureSinkTriggers(trig =>
        {
            trig.EmitEveryNExecutions = 10;
            trig.EmitOnFailure = true;
        });

        // Assert
        options.Sinks.Should().HaveCount(2);
        options.Sinks.Should().Contain(s => s.Name == "Console");
        options.Sinks.Should().Contain(s => s.Name == "Observable");
        options.SinkTriggers.EmitEveryNExecutions.Should().Be(10);
        options.SinkTriggers.EmitOnFailure.Should().BeTrue();
        observable.Should().NotBeNull();
    }

    [Fact]
    public void DependencyInjection_AddMetricFlow_WiresConsoleSink()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var services = new ServiceCollection();

        services.AddMetricFlow(opt =>
        {
            opt.Topic = "WebApp";
        })
        .AddConsoleSink(opt =>
        {
            opt.OutputWriter = stringWriter;
            opt.Colorize = false;
            opt.IncludeTimestamp = false;
        });

        using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<IMetricTracker>();

        // Act
        tracker.RegisterSink(new RecordingMetricSink("Custom"));
        tracker.FlushSinks();

        // Assert
        tracker.GetSinks().Should().Contain(s => s.Name == "Console");
        tracker.GetSinks().Should().Contain(s => s.Name == "Custom");
    }

    [Fact]
    public void LoggerMetricSink_EmitsSnapshot_UsesInformationLogLevelByDefault()
    {
        // Arrange
        var testLogger = new TestLogger();
        var sink = new LoggerMetricSink(testLogger, new LoggerMetricSinkOptions
        {
            Prefix = "[AppLogger]"
        });

        var snapshot = new ThroughputSnapshot(
            MetricName: "OrderIngest",
            CounterName: "Throughput",
            TotalItems: 500,
            TotalOperations: 10,
            TotalDuration: TimeSpan.FromSeconds(1),
            ItemsPerSecond: 500.0,
            AverageItemsPerOperation: 50.0,
            Timestamp: DateTime.UtcNow,
            FailedOperations: 0);

        // Act
        sink.Emit(new[] { snapshot });

        // Assert
        testLogger.Logs.Should().HaveCount(1);
        var entry = testLogger.Logs[0];
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("[AppLogger]");
        entry.Message.Should().Contain("[Throughput:OrderIngest]");
        entry.Message.Should().Contain("TotalItems: 500");
        entry.Message.Should().Contain("Rate: 500.0 items/s");
    }

    [Fact]
    public void LoggerMetricSink_WithFailures_UsesFailureLogLevel()
    {
        // Arrange
        var testLogger = new TestLogger();
        var sink = new LoggerMetricSink(testLogger, new LoggerMetricSinkOptions
        {
            FailureLogLevel = LogLevel.Error
        });

        var failureSnapshot = new FailureSnapshot(
            MetricName: "ProcessPayment",
            CounterName: "Failure",
            TotalOperations: 10,
            TotalFailures: 2,
            LogicalFailures: 1,
            ExceptionFailures: 1,
            Timestamp: DateTime.UtcNow);

        // Act
        sink.Emit(new[] { failureSnapshot });

        // Assert
        testLogger.Logs.Should().HaveCount(1);
        var entry = testLogger.Logs[0];
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("[Failure:ProcessPayment]");
        entry.Message.Should().Contain("Failed: 2 (20.0%)");
    }

    [Fact]
    public void DependencyInjection_AddMetricFlow_WiresLoggerSink()
    {
        // Arrange
        var testLogger = new TestLogger();
        var services = new ServiceCollection();

        services.AddMetricFlow(opt =>
        {
            opt.Topic = "BillingService";
        })
        .AddLoggerSink(testLogger, opt =>
        {
            opt.Prefix = "[Billing]";
        });

        using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<IMetricTracker>();

        // Act
        tracker.FlushSinks();

        // Assert
        tracker.GetSinks().Should().Contain(s => s.Name == "Logger");
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

    private sealed class RecordingMetricSink : IMetricSink
    {
        public string Name { get; }
        public List<IReadOnlyList<IMetricSnapshot>> EmittedBatches { get; } = new();

        public RecordingMetricSink(string name = "RecordingSink")
        {
            Name = name;
        }

        public void Emit(IReadOnlyList<IMetricSnapshot> snapshots)
        {
            EmittedBatches.Add(snapshots);
        }
    }
}
