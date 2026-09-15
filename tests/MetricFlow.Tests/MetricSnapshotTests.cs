using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Extensions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace MetricFlow.Tests;

public class MetricSnapshotTests
{
    private class CustomSnapshot(string metricName, string counterName, DateTime timestamp, string formatted)
        : IMetricSnapshot
    {
        public string MetricName { get; } = metricName;
        public string CounterName { get; } = counterName;
        public DateTime Timestamp { get; } = timestamp;
        public string ToFormattedString() => formatted;
    }

    [Fact]
    public void CustomSnapshot_ShouldExposeIMetricSnapshotProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        IMetricSnapshot snapshot = new CustomSnapshot("CustomOp", "CustomCounter", now, "CustomOutput");

        // Assert
        snapshot.MetricName.Should().Be("CustomOp");
        snapshot.CounterName.Should().Be("CustomCounter");
        snapshot.Timestamp.Should().Be(now);
        snapshot.ToFormattedString().Should().Be("CustomOutput");
    }

    #region DurationSnapshot Tests

    [Fact]
    public void DurationSnapshot_ShouldImplementIMetricSnapshot_AndExposeProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshot = new DurationSnapshot(
            MetricName: "OrderService.Checkout",
            CounterName: "Duration",
            InCount: 10,
            OutCount: 9,
            FailedCount: 1,
            TotalDuration: TimeSpan.FromMilliseconds(450),
            AverageDuration: TimeSpan.FromMilliseconds(50),
            MinDuration: TimeSpan.FromMilliseconds(10),
            MaxDuration: TimeSpan.FromMilliseconds(120),
            Timestamp: now
        );

        // Assert interface implementation
        IMetricSnapshot metricSnapshot = snapshot;
        metricSnapshot.MetricName.Should().Be("OrderService.Checkout");
        metricSnapshot.CounterName.Should().Be("Duration");
        metricSnapshot.Timestamp.Should().Be(now);

        // Assert typed record properties
        snapshot.InCount.Should().Be(10);
        snapshot.OutCount.Should().Be(9);
        snapshot.FailedCount.Should().Be(1);
        snapshot.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(450));
        snapshot.AverageDuration.Should().Be(TimeSpan.FromMilliseconds(50));
        snapshot.MinDuration.Should().Be(TimeSpan.FromMilliseconds(10));
        snapshot.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(120));
    }

    [Fact]
    public void DurationSnapshot_ToFormattedString_ShouldIncludeAllKeyInformation()
    {
        // Arrange
        var snapshot = new DurationSnapshot(
            MetricName: "ProcessPayment",
            CounterName: "Duration",
            InCount: 5,
            OutCount: 5,
            FailedCount: 2,
            TotalDuration: TimeSpan.FromMilliseconds(150.5),
            AverageDuration: TimeSpan.FromMilliseconds(30.1),
            MinDuration: TimeSpan.FromMilliseconds(12.3),
            MaxDuration: TimeSpan.FromMilliseconds(65.4),
            Timestamp: DateTime.UtcNow
        );

        // Act
        var formatted = snapshot.ToFormattedString();
        var toStringResult = snapshot.ToString();

        // Assert
        formatted.Should().Contain("[Duration] Metric: ProcessPayment");
        formatted.Should().Contain($"Duration (min, max, avg): {snapshot.MinDuration.TotalMilliseconds:F2} ms / {snapshot.MaxDuration.TotalMilliseconds:F2} ms / {snapshot.AverageDuration.TotalMilliseconds:F2} ms");
        formatted.Should().Contain($"Total duration: {snapshot.TotalDuration.TotalMilliseconds:F2} ms");
        toStringResult.Should().Be(formatted);
    }

    [Fact]
    public void DurationSnapshot_RecordEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var snapshot1 = new DurationSnapshot("Op", "Duration", 1, 1, 0, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), timestamp);
        var snapshot2 = new DurationSnapshot("Op", "Duration", 1, 1, 0, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), timestamp);
        var snapshot3 = snapshot1 with { InCount = 2 };

        // Assert
        snapshot1.Should().Be(snapshot2);
        snapshot1.Should().NotBe(snapshot3);
    }

    #endregion

    #region MemorySnapshot Tests

    [Fact]
    public void MemorySnapshot_ShouldImplementIMetricSnapshot_AndExposeProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshot = new MemorySnapshot(
            MetricName: "DataProcessing",
            CounterName: "Memory",
            OperationCount: 8,
            TotalAllocatedBytes: 8192,
            AverageAllocatedBytes: 1024,
            MinAllocatedBytes: 512,
            MaxAllocatedBytes: 2048,
            Timestamp: now
        );

        // Assert interface implementation
        IMetricSnapshot metricSnapshot = snapshot;
        metricSnapshot.MetricName.Should().Be("DataProcessing");
        metricSnapshot.CounterName.Should().Be("Memory");
        metricSnapshot.Timestamp.Should().Be(now);

        // Assert record properties
        snapshot.OperationCount.Should().Be(8);
        snapshot.TotalAllocatedBytes.Should().Be(8192);
        snapshot.AverageAllocatedBytes.Should().Be(1024);
        snapshot.MinAllocatedBytes.Should().Be(512);
        snapshot.MaxAllocatedBytes.Should().Be(2048);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2048)]
    [InlineData(5 * 1024 * 1024)]
    public void MemorySnapshot_ToFormattedString_ShouldFormatByteSizesCorrectly(long allocatedBytes)
    {
        // Arrange
        var snapshot = new MemorySnapshot(
            MetricName: "MemoryOp",
            CounterName: "Memory",
            OperationCount: 1,
            TotalAllocatedBytes: allocatedBytes,
            AverageAllocatedBytes: allocatedBytes,
            MinAllocatedBytes: allocatedBytes,
            MaxAllocatedBytes: allocatedBytes,
            Timestamp: DateTime.UtcNow
        );

        string expectedAllocated = allocatedBytes < 1024
            ? $"{allocatedBytes} B"
            : allocatedBytes < 1024 * 1024
                ? $"{allocatedBytes / 1024.0:F2} KB"
                : $"{allocatedBytes / (1024.0 * 1024.0):F2} MB";

        // Act
        var formatted = snapshot.ToFormattedString();
        var toStringResult = snapshot.ToString();

        // Assert
        formatted.Should().Contain("[Memory] Metric: MemoryOp");
        formatted.Should().Contain($"Total allocated: {expectedAllocated}");
        toStringResult.Should().Be(formatted);
    }

    [Fact]
    public void MemorySnapshot_RecordEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var snapshot1 = new MemorySnapshot("Op", "Memory", 2, 2048, 1024, 512, 1536, timestamp);
        var snapshot2 = new MemorySnapshot("Op", "Memory", 2, 2048, 1024, 512, 1536, timestamp);
        var snapshot3 = snapshot1 with { OperationCount = 5 };

        // Assert
        snapshot1.Should().Be(snapshot2);
        snapshot1.Should().NotBe(snapshot3);
    }

    #endregion

    #region ExceptionSnapshot Tests

    [Fact]
    public void ExceptionSnapshot_ShouldImplementIMetricSnapshot_AndExposeProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var breakdown = new Dictionary<string, long>
        {
            { nameof(InvalidOperationException), 3 },
            { nameof(ArgumentNullException), 1 }
        };

        var snapshot = new ExceptionSnapshot(
            MetricName: "DatabaseQuery",
            CounterName: "Exception",
            TotalOperations: 20,
            TotalFailures: 4,
            ExceptionsByType: breakdown,
            Timestamp: now
        );

        // Assert interface implementation
        IMetricSnapshot metricSnapshot = snapshot;
        metricSnapshot.MetricName.Should().Be("DatabaseQuery");
        metricSnapshot.CounterName.Should().Be("Exception");
        metricSnapshot.Timestamp.Should().Be(now);

        // Assert record properties
        snapshot.TotalOperations.Should().Be(20);
        snapshot.TotalFailures.Should().Be(4);
        snapshot.FailureRate.Should().Be(0.2); // 4 / 20
        snapshot.ExceptionsByType.Should().BeEquivalentTo(breakdown);
    }

    [Fact]
    public void ExceptionSnapshot_FailureRate_WhenTotalOperationsIsZero_ShouldReturnZero()
    {
        // Arrange
        var snapshot = new ExceptionSnapshot(
            MetricName: "IdleOp",
            CounterName: "Exception",
            TotalOperations: 0,
            TotalFailures: 0,
            ExceptionsByType: new Dictionary<string, long>(),
            Timestamp: DateTime.UtcNow
        );

        // Assert
        snapshot.FailureRate.Should().Be(0.0);
    }

    [Fact]
    public void ExceptionSnapshot_ToFormattedString_WithExceptions_ShouldIncludeBreakdown()
    {
        // Arrange
        var breakdown = new Dictionary<string, long>
        {
            { "TimeoutException", 2 },
            { "HttpRequestException", 1 }
        };

        var snapshot = new ExceptionSnapshot(
            MetricName: "HttpCall",
            CounterName: "Exception",
            TotalOperations: 10,
            TotalFailures: 3,
            ExceptionsByType: breakdown,
            Timestamp: DateTime.UtcNow
        );

        // Act
        var formatted = snapshot.ToFormattedString();
        var toStringResult = snapshot.ToString();

        // Assert
        formatted.Should().Contain("[Exception] Metric: HttpCall");
        formatted.Should().Contain($"Failures / Operations: 3 / 10 ({snapshot.FailureRate:P2})");
        formatted.Should().Contain("Exceptions Breakdown:");
        formatted.Should().Contain("  - TimeoutException: 2");
        formatted.Should().Contain("  - HttpRequestException: 1");
        toStringResult.Should().Be(formatted);
    }

    [Fact]
    public void ExceptionSnapshot_ToFormattedString_WithoutExceptions_ShouldNotIncludeBreakdown()
    {
        // Arrange
        var snapshot = new ExceptionSnapshot(
            MetricName: "SuccessfulOp",
            CounterName: "Exception",
            TotalOperations: 10,
            TotalFailures: 0,
            ExceptionsByType: new Dictionary<string, long>(),
            Timestamp: DateTime.UtcNow
        );

        // Act
        var formatted = snapshot.ToFormattedString();

        // Assert
        formatted.Should().Contain("[Exception] Metric: SuccessfulOp");
        formatted.Should().Contain($"Failures / Operations: 0 / 10 ({snapshot.FailureRate:P2})");
        formatted.Should().NotContain("Exceptions Breakdown:");
    }

    [Fact]
    public void ExceptionSnapshot_RecordEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var dict = new Dictionary<string, long> { { "Ex", 1 } };
        var snapshot1 = new ExceptionSnapshot("Op", "Exception", 5, 1, dict, timestamp);
        var snapshot2 = new ExceptionSnapshot("Op", "Exception", 5, 1, dict, timestamp);
        var snapshot3 = snapshot1 with { TotalFailures = 2 };

        // Assert
        snapshot1.Should().Be(snapshot2);
        snapshot1.Should().NotBe(snapshot3);
    }

    #endregion

    #region MetricSnapshotExtensions Tests

    [Fact]
    public void ToFormattedString_WhenSnapshotsIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<IMetricSnapshot> snapshots = null!;

        // Act
        var act = () => snapshots.ToFormattedString();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToFormattedString_WhenSnapshotsIsEmpty_ShouldReturnEmptyString()
    {
        // Arrange
        var snapshots = Enumerable.Empty<IMetricSnapshot>();

        // Act
        var result = snapshots.ToFormattedString();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToFormattedString_ShouldGroupSnapshotsByMetricNameCaseInsensitively()
    {
        // Arrange
        var snapshot1 = new CustomSnapshot("MyMetric", "CounterA", DateTime.UtcNow, "OutputA");
        var snapshot2 = new CustomSnapshot("mymetric", "CounterB", DateTime.UtcNow, "OutputB");
        var snapshot3 = new CustomSnapshot("OtherMetric", "CounterA", DateTime.UtcNow, "OutputC");

        var list = new List<IMetricSnapshot> { snapshot1, snapshot2, snapshot3 };

        // Act
        var result = list.ToFormattedString();

        // Assert
        // Both snapshot1 and snapshot2 should be formatted together in the first group
        result.Should().Contain("OutputA");
        result.Should().Contain("OutputB");
        result.Should().Contain("OutputC");

        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        lines[0].Should().Be("OutputA");
        lines[1].Should().Be("OutputB");
        lines[2].Should().Be("OutputC");
    }

    [Fact]
    public void ToFormattedString_WithSnapshotsSource_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IMetricSnapshotsSource source = null!;

        // Act
        var act = () => source.ToFormattedString();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToFormattedString_WithSnapshotsSource_ShouldReturnFormattedStringFromSource()
    {
        // Arrange
        var source = Substitute.For<IMetricSnapshotsSource>();
        var snapshots = new List<IMetricSnapshot>
        {
            new CustomSnapshot("OperationA", "Duration", DateTime.UtcNow, "[Duration] OpA"),
            new CustomSnapshot("OperationA", "Memory", DateTime.UtcNow, "[Memory] OpA")
        };
        source.GetAllSnapshots().Returns(snapshots);

        // Act
        var result = source.ToFormattedString();

        // Assert
        result.Should().Contain("[Duration] OpA");
        result.Should().Contain("[Memory] OpA");
        source.Received(1).GetAllSnapshots();
    }

    [Fact]
    public void ToFormattedString_WithMetricTracker_ShouldFormatAllSnapshots()
    {
        // Arrange
        var tracker = new MetricTracker("SnapshotTopic")
            .AddMemoryCounter();

        tracker.In("OrderProcess");
        tracker.Out("OrderProcess");

        // Act
        var result = tracker.ToFormattedString();

        // Assert
        result.Should().Contain("[Duration] Metric: OrderProcess");
        result.Should().Contain("[Memory] Metric: OrderProcess");
    }

    #endregion
}
