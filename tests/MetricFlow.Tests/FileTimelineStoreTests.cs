using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class FileTimelineStoreTests : IDisposable
{
    private readonly string _testDir;

    public FileTimelineStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MetricFlow_FileTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task EmitAsync_WritesPartitionedNDJSON_And_ReadsBackAccurately()
    {
        // Arrange
        var options = new FileTimelineStoreOptions
        {
            DirectoryPath = _testDir,
            PartitionByInstance = true,
            AutoFlush = true
        };

        var store = new FileTimelineStore(options);
        var baseTime = DateTimeOffset.UtcNow;
        var resource = new ResourceMetadata("BillingSvc", "pod-42", "pod-42", "Production");

        var snapshot = new DurationSnapshot(
            MetricName: "ProcessInvoice",
            CounterName: "Duration",
            InCount: 12,
            OutCount: 12,
            FailedCount: 1,
            TotalDuration: TimeSpan.FromMilliseconds(120),
            AverageDuration: TimeSpan.FromMilliseconds(10),
            MinDuration: TimeSpan.FromMilliseconds(4),
            MaxDuration: TimeSpan.FromMilliseconds(22),
            Timestamp: baseTime.UtcDateTime
        );

        var entry = new MetricTimelineEntry(
            PeriodStart: baseTime.AddMinutes(-5),
            PeriodEnd: baseTime,
            Cumulative: snapshot,
            Delta: snapshot,
            Resource: resource
        );

        // Act
        await store.EmitAsync(new[] { entry });

        // Assert file exists in partitioned instance directory
        var files = Directory.GetFiles(_testDir, "*.ndjson", SearchOption.AllDirectories);
        files.Should().HaveCount(1);
        files[0].Should().Contain("pod-42");

        // Act - Query back
        var queried = await store.GetTimelineAsync(baseTime.AddMinutes(-10), baseTime.AddMinutes(5));

        // Assert
        queried.Should().HaveCount(1);
        var returned = queried[0];
        returned.MetricName.Should().Be("ProcessInvoice");
        returned.Resource.Should().NotBeNull();
        returned.Resource!.InstanceId.Should().Be("pod-42");

        var dur = returned.Delta.Should().BeOfType<DurationSnapshot>().Subject;
        dur.OutCount.Should().Be(12);
        dur.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(120));
    }

    [Fact]
    public async Task GetAggregatedSnapshotsAsync_CombinesDeltasAcrossMultipleFiles()
    {
        // Arrange
        var options = new FileTimelineStoreOptions
        {
            DirectoryPath = _testDir,
            PartitionByInstance = true
        };

        var store = new FileTimelineStore(options);
        var baseTime = DateTimeOffset.UtcNow;

        // Instance 1
        var res1 = new ResourceMetadata("OrderSvc", "instance-1");
        var snap1 = new DurationSnapshot("CreateOrder", "Duration", 5, 5, 0, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(15), baseTime.UtcDateTime);
        var entry1 = new MetricTimelineEntry(baseTime.AddMinutes(-10), baseTime.AddMinutes(-5), snap1, snap1, res1);

        // Instance 2
        var res2 = new ResourceMetadata("OrderSvc", "instance-2");
        var snap2 = new DurationSnapshot("CreateOrder", "Duration", 10, 10, 1, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(15), TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(25), baseTime.UtcDateTime);
        var entry2 = new MetricTimelineEntry(baseTime.AddMinutes(-5), baseTime, snap2, snap2, res2);

        await store.EmitAsync(new[] { entry1, entry2 });

        // Act - Cluster-wide aggregated snapshot query across both instances
        var aggregated = await store.GetAggregatedSnapshotsAsync(baseTime.AddMinutes(-15), baseTime.AddMinutes(1), "CreateOrder");

        // Assert
        aggregated.Should().HaveCount(1);
        var dur = aggregated[0].Should().BeOfType<DurationSnapshot>().Subject;
        dur.OutCount.Should().Be(15); // 5 + 10
        dur.FailedCount.Should().Be(1);
        dur.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(200)); // 50 + 150
        dur.MinDuration.Should().Be(TimeSpan.FromMilliseconds(5));
        dur.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(25));
    }
}
