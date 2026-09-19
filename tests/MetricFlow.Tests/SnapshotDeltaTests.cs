using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Extensions;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class SnapshotDeltaTests
{
    [Fact]
    public void ComputeDelta_ForDurationSnapshot_ComputesExactDifference()
    {
        // Arrange
        var prev = new DurationSnapshot(
            MetricName: "OrderCheckout",
            CounterName: "Duration",
            InCount: 10,
            OutCount: 10,
            FailedCount: 1,
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AverageDuration: TimeSpan.FromMilliseconds(10),
            MinDuration: TimeSpan.FromMilliseconds(5),
            MaxDuration: TimeSpan.FromMilliseconds(20),
            Timestamp: DateTime.UtcNow.AddMinutes(-1)
        );

        var cur = new DurationSnapshot(
            MetricName: "OrderCheckout",
            CounterName: "Duration",
            InCount: 15,
            OutCount: 15,
            FailedCount: 3,
            TotalDuration: TimeSpan.FromMilliseconds(175),
            AverageDuration: TimeSpan.FromMilliseconds(11.66),
            MinDuration: TimeSpan.FromMilliseconds(4),
            MaxDuration: TimeSpan.FromMilliseconds(30),
            Timestamp: DateTime.UtcNow
        );

        // Act
        var delta = (DurationSnapshot)cur.ComputeDelta(prev);

        // Assert
        delta.MetricName.Should().Be("OrderCheckout");
        delta.InCount.Should().Be(5);
        delta.OutCount.Should().Be(5);
        delta.FailedCount.Should().Be(2);
        delta.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(75));
        delta.AverageDuration.Should().Be(TimeSpan.FromMilliseconds(15)); // 75ms / 5
    }

    [Fact]
    public void ComputeDelta_ForMemorySnapshot_ComputesExactDifference()
    {
        // Arrange
        var prev = new MemorySnapshot(
            MetricName: "ImageResize",
            CounterName: "Memory",
            OperationCount: 5,
            TotalAllocatedBytes: 50_000,
            AverageAllocatedBytes: 10_000,
            MinAllocatedBytes: 8_000,
            MaxAllocatedBytes: 12_000,
            Timestamp: DateTime.UtcNow.AddMinutes(-1)
        );

        var cur = new MemorySnapshot(
            MetricName: "ImageResize",
            CounterName: "Memory",
            OperationCount: 8,
            TotalAllocatedBytes: 95_000,
            AverageAllocatedBytes: 11_875,
            MinAllocatedBytes: 7_000,
            MaxAllocatedBytes: 20_000,
            Timestamp: DateTime.UtcNow
        );

        // Act
        var delta = (MemorySnapshot)cur.ComputeDelta(prev);

        // Assert
        delta.OperationCount.Should().Be(3);
        delta.TotalAllocatedBytes.Should().Be(45_000);
        delta.AverageAllocatedBytes.Should().Be(15_000); // 45000 / 3
    }

    [Fact]
    public void ComputeDelta_ForExceptionSnapshot_ComputesDictionaryDiff()
    {
        // Arrange
        var prevBreakdown = new Dictionary<string, long>
        {
            ["TimeoutException"] = 2,
            ["HttpRequestException"] = 1
        };
        var prev = new ExceptionSnapshot("FetchData", "Exception", 20, 3, prevBreakdown, DateTime.UtcNow.AddMinutes(-1));

        var curBreakdown = new Dictionary<string, long>
        {
            ["TimeoutException"] = 5,
            ["HttpRequestException"] = 1,
            ["TaskCanceledException"] = 2
        };
        var cur = new ExceptionSnapshot("FetchData", "Exception", 30, 8, curBreakdown, DateTime.UtcNow);

        // Act
        var delta = (ExceptionSnapshot)cur.ComputeDelta(prev);

        // Assert
        delta.TotalOperations.Should().Be(10);
        delta.TotalFailures.Should().Be(5);
        delta.ExceptionsByType.Should().ContainKey("TimeoutException").WhoseValue.Should().Be(3);
        delta.ExceptionsByType.Should().NotContainKey("HttpRequestException"); // 0 delta
        delta.ExceptionsByType.Should().ContainKey("TaskCanceledException").WhoseValue.Should().Be(2);
    }

    [Fact]
    public void AggregateSnapshots_CombinesMultipleIntervalsAndInstances()
    {
        // Arrange
        var slice1 = new DurationSnapshot(
            MetricName: "ProcessOrder",
            CounterName: "Duration",
            InCount: 10,
            OutCount: 10,
            FailedCount: 1,
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AverageDuration: TimeSpan.FromMilliseconds(10),
            MinDuration: TimeSpan.FromMilliseconds(5),
            MaxDuration: TimeSpan.FromMilliseconds(25),
            Timestamp: DateTime.UtcNow.AddMinutes(-10)
        );

        var slice2 = new DurationSnapshot(
            MetricName: "ProcessOrder",
            CounterName: "Duration",
            InCount: 20,
            OutCount: 20,
            FailedCount: 2,
            TotalDuration: TimeSpan.FromMilliseconds(300),
            AverageDuration: TimeSpan.FromMilliseconds(15),
            MinDuration: TimeSpan.FromMilliseconds(3),
            MaxDuration: TimeSpan.FromMilliseconds(40),
            Timestamp: DateTime.UtcNow
        );

        // Act
        var aggregated = new[] { slice1, slice2 }.AggregateSnapshots();

        // Assert
        aggregated.Should().HaveCount(1);
        var dur = aggregated[0].Should().BeOfType<DurationSnapshot>().Subject;
        dur.InCount.Should().Be(30);
        dur.OutCount.Should().Be(30);
        dur.FailedCount.Should().Be(3);
        dur.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(400));
        dur.AverageDuration.Should().Be(TimeSpan.FromTicks(TimeSpan.FromMilliseconds(400).Ticks / 30));
        dur.MinDuration.Should().Be(TimeSpan.FromMilliseconds(3));
        dur.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(40));
    }
}
