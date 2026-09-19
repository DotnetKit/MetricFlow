using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class MemoryRingBufferTimelineStoreTests
{
    [Fact]
    public async Task EmitAsync_And_GetTimelineAsync_FiltersByTimeRangeAndMetric()
    {
        // Arrange
        var store = new MemoryRingBufferTimelineStore(maxCapacity: 100);
        var baseTime = DateTimeOffset.UtcNow;

        var entries = new List<MetricTimelineEntry>
        {
            CreateEntry("MetricA", baseTime.AddMinutes(-30), baseTime.AddMinutes(-20), 5),
            CreateEntry("MetricB", baseTime.AddMinutes(-20), baseTime.AddMinutes(-10), 10),
            CreateEntry("MetricA", baseTime.AddMinutes(-10), baseTime, 15),
        };

        // Act
        await store.EmitAsync(entries);

        // Query between -25min and -15min (matches entries 1 and 2, but not entry 3 which starts at -10min)
        var queried = await store.GetTimelineAsync(baseTime.AddMinutes(-25), baseTime.AddMinutes(-15));

        // Assert
        queried.Should().HaveCount(2); // Should include MetricA (-30 to -20) and MetricB (-20 to -10)

        // Query MetricA specifically
        var metricAEntries = await store.GetTimelineAsync(baseTime.AddMinutes(-40), baseTime, "MetricA");
        metricAEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAggregatedSnapshotsAsync_RollsUpIntervalDeltas()
    {
        // Arrange
        var store = new MemoryRingBufferTimelineStore();
        var baseTime = DateTimeOffset.UtcNow;

        var entries = new List<MetricTimelineEntry>
        {
            CreateEntry("Payment", baseTime.AddMinutes(-20), baseTime.AddMinutes(-10), 10),
            CreateEntry("Payment", baseTime.AddMinutes(-10), baseTime, 20)
        };

        await store.EmitAsync(entries);

        // Act - Query aggregated snapshot
        var aggregated = await store.GetAggregatedSnapshotsAsync(baseTime.AddMinutes(-25), baseTime, "Payment");

        // Assert
        aggregated.Should().HaveCount(1);
        var dur = aggregated[0].Should().BeOfType<DurationSnapshot>().Subject;
        dur.OutCount.Should().Be(30); // 10 + 20
    }

    [Fact]
    public async Task TrimExcess_EnforcesMaxCapacity()
    {
        // Arrange
        var store = new MemoryRingBufferTimelineStore(maxCapacity: 5);
        var baseTime = DateTimeOffset.UtcNow;

        var entries = Enumerable.Range(1, 10)
            .Select(i => CreateEntry($"Metric_{i}", baseTime.AddMinutes(-i), baseTime.AddMinutes(-i + 1), i))
            .ToList();

        // Act
        await store.EmitAsync(entries);

        // Assert
        store.Count.Should().Be(5);
    }

    private static MetricTimelineEntry CreateEntry(string name, DateTimeOffset start, DateTimeOffset end, long count)
    {
        var snapshot = new DurationSnapshot(
            MetricName: name,
            CounterName: "Duration",
            InCount: count,
            OutCount: count,
            FailedCount: 0,
            TotalDuration: TimeSpan.FromMilliseconds(count * 10),
            AverageDuration: TimeSpan.FromMilliseconds(10),
            MinDuration: TimeSpan.FromMilliseconds(5),
            MaxDuration: TimeSpan.FromMilliseconds(15),
            Timestamp: end.UtcDateTime
        );

        return new MetricTimelineEntry(
            PeriodStart: start,
            PeriodEnd: end,
            Cumulative: snapshot,
            Delta: snapshot
        );
    }
}
