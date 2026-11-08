using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class OperationSummaryTests
{
    [Fact]
    public void GetOperationSummaries_AggregatesDifferentCountersUnderSameOperation()
    {
        var now = DateTime.UtcNow;
        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                MetricName: "Stage1_Process",
                CounterName: "Duration",
                InCount: 100,
                OutCount: 100,
                FailedCount: 2,
                AverageDuration: TimeSpan.FromMilliseconds(15),
                MinDuration: TimeSpan.FromMilliseconds(5),
                MaxDuration: TimeSpan.FromMilliseconds(45),
                TotalDuration: TimeSpan.FromMilliseconds(1500),
                Timestamp: now
            ),
            new ThroughputSnapshot(
                MetricName: "Stage1_Process",
                CounterName: "Throughput",
                TotalItems: 5000,
                TotalOperations: 100,
                ItemsPerSecond: 2500.0,
                AverageItemsPerOperation: 50.0,
                TotalDuration: TimeSpan.FromSeconds(2),
                Timestamp: now
            ),
            new MemorySnapshot(
                MetricName: "Stage1_Process",
                CounterName: "Memory",
                OperationCount: 100,
                TotalAllocatedBytes: 102400,
                AverageAllocatedBytes: 1024,
                MinAllocatedBytes: 512,
                MaxAllocatedBytes: 2048,
                Timestamp: now
            ),
            new FailureSnapshot(
                MetricName: "Stage1_Process",
                CounterName: "Failure",
                TotalOperations: 100,
                TotalFailures: 2,
                LogicalFailures: 2,
                ExceptionFailures: 0,
                Timestamp: now
            ),
            new DimensionSnapshot(
                MetricName: "Stage1_Process",
                CounterName: "Dimension",
                DimensionName: "status",
                TotalOperations: 100,
                TaggedOperations: 100,
                UntaggedOperations: 0,
                FailedOperations: 2,
                Breakdown: new Dictionary<string, long> { ["Success"] = 98, ["Error"] = 2 },
                Timestamp: now
            ),
        };

        var summaries = snapshots.GetOperationSummaries();

        summaries.Should().HaveCount(1);
        var s1 = summaries[0];

        s1.OperationName.Should().Be("Stage1_Process");
        s1.TotalOperations.Should().Be(100);
        s1.TotalFailures.Should().Be(2);
        s1.FailureRate.Should().Be(0.02);
        s1.Duration.Should().NotBeNull();
        s1.Throughput.Should().NotBeNull();
        s1.Memory.Should().NotBeNull();
        s1.Failure.Should().NotBeNull();
        s1.Dimensions.Should().HaveCount(1);
        s1.Dimensions[0].DimensionName.Should().Be("status");
    }

    [Fact]
    public void GetOperationSummaries_PreservesOrderAcrossMultipleOperations()
    {
        var now = DateTime.UtcNow;
        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                "StageB",
                "Duration",
                10,
                10,
                0,
                TimeSpan.FromMilliseconds(5),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(50),
                now
            ),
            new DurationSnapshot(
                "StageA",
                "Duration",
                20,
                20,
                0,
                TimeSpan.FromMilliseconds(8),
                TimeSpan.FromMilliseconds(2),
                TimeSpan.FromMilliseconds(12),
                TimeSpan.FromMilliseconds(160),
                now
            ),
            new ThroughputSnapshot(
                "StageB",
                "Throughput",
                100,
                10,
                TimeSpan.FromSeconds(2),
                50.0,
                10.0,
                now
            ),
        };

        var summaries = snapshots.GetOperationSummaries();

        summaries.Should().HaveCount(2);
        summaries[0].OperationName.Should().Be("StageB");
        summaries[0].Throughput.Should().NotBeNull();
        summaries[1].OperationName.Should().Be("StageA");
    }

    [Fact]
    public void GetOperationSummary_WithTracker_ReturnsCorrelatedOperation()
    {
        using var tracker = new MetricTracker("TestTopic")
            .AddThroughputCounter()
            .AddMemoryCounter();

        using (tracker.TrackItems("FetchStage", 50))
        {
            // simulated work
        }

        var op = tracker.GetOperationSummary("FetchStage");
        op.Should().NotBeNull();
        op.OperationName.Should().Be("FetchStage");
        op.TotalOperations.Should().Be(1);
        op.Throughput.Should().NotBeNull();
        op.Throughput!.TotalItems.Should().Be(50);
        op.Duration.Should().NotBeNull();
    }

    [Fact]
    public void GetOperationSummary_NonExistent_ReturnsNull()
    {
        using var tracker = new MetricTracker("TestTopic");
        tracker.GetOperationSummary("MissingOp").Should().BeNull();
    }

    [Fact]
    public void Tracker_GetOperationSummaries_PreservesRegistrationOrderOfOperations()
    {
        using var tracker = new MetricTracker("OrderPreservationTopic")
            .AddThroughputCounter()
            .AddMemoryCounter()
            .AddFailureCounter()
            .AddExceptionCounter()
            .AddDimensionCounter("entity_type");

        string[] expectedOrder =
        [
            "RunSample",
            "Add first entity",
            "Validate entity",
            "Persist to store",
            "Publish event",
            "Cleanup resources",
        ];

        foreach (var opName in expectedOrder)
        {
            using (tracker.Track(opName, new() { ["entity_type"] = "User" }))
            {
                // Track first time in registration order
            }
        }

        // Track again in reverse order to ensure initial registration order is preserved
        for (var i = expectedOrder.Length - 1; i >= 0; i--)
        {
            using (tracker.TrackItems(expectedOrder[i], 10, new() { ["entity_type"] = "Order" }))
            {
                // Subsequent tracking
            }
        }

        var summaries = tracker.GetOperationSummaries();
        summaries.Select(s => s.OperationName).Should().ContainInOrder(expectedOrder);
        summaries.Select(s => s.OperationName).Should().Equal(expectedOrder);

        // Also verify individual counter GetAllSnapshots() preserve insertion sequence
        foreach (var counter in tracker.GetCounters())
        {
            counter
                .GetAllSnapshots()
                .Select(s => s.MetricName)
                .Should()
                .Equal(expectedOrder, $"Counter '{counter.Name}' should preserve insertion sequence");
        }
    }

    [Fact]
    public void ConsoleMetricSink_TableMode_ListsOperationsInRegistrationOrder()
    {
        using var writer = new StringWriter();
        using var tracker = new MetricTracker("TableOrderTopic")
            .AddThroughputCounter()
            .AddMemoryCounter()
            .AddFailureCounter()
            .AddConsoleSink(opt =>
            {
                opt.OutputWriter = writer;
                opt.DisplayMode = ConsoleDisplayMode.Table;
                opt.Colorize = false;
                opt.IncludeTimestamp = false;
            });

        string[] operations =
        [
            "RunSample",
            "Add first entity",
            "Fetch dependencies",
            "Finalize batch",
        ];

        foreach (var op in operations)
        {
            using (tracker.TrackItems(op, 5)) { }
        }

        tracker.FlushSinks();

        var output = writer.ToString();
        var runSampleIdx = output.IndexOf("RunSample", StringComparison.Ordinal);
        var addFirstEntityIdx = output.IndexOf("Add first entity", StringComparison.Ordinal);
        var fetchDepsIdx = output.IndexOf("Fetch dependencies", StringComparison.Ordinal);
        var finalizeBatchIdx = output.IndexOf("Finalize batch", StringComparison.Ordinal);

        runSampleIdx.Should().BeGreaterThanOrEqualTo(0);
        addFirstEntityIdx.Should().BeGreaterThan(runSampleIdx);
        fetchDepsIdx.Should().BeGreaterThan(addFirstEntityIdx);
        finalizeBatchIdx.Should().BeGreaterThan(fetchDepsIdx);
    }
}
