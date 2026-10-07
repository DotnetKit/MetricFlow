using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
