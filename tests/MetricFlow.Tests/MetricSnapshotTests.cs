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
            TotalExceptions: 4,
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
        snapshot.TotalExceptions.Should().Be(4);
        snapshot.TotalFailures.Should().Be(4); // backward compat
        snapshot.ExceptionRate.Should().Be(0.2); // 4 / 20
        snapshot.FailureRate.Should().Be(0.2);
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
            TotalExceptions: 0,
            ExceptionsByType: new Dictionary<string, long>(),
            Timestamp: DateTime.UtcNow
        );

        // Assert
        snapshot.ExceptionRate.Should().Be(0.0);
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
            TotalExceptions: 3,
            ExceptionsByType: breakdown,
            Timestamp: DateTime.UtcNow
        );

        // Act
        var formatted = snapshot.ToFormattedString();
        var toStringResult = snapshot.ToString();

        // Assert
        formatted.Should().Contain("[Exception] Metric: HttpCall");
        formatted.Should().Contain($"Exceptions / Operations: 3 / 10 ({snapshot.ExceptionRate:P2})");
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
            TotalExceptions: 0,
            ExceptionsByType: new Dictionary<string, long>(),
            Timestamp: DateTime.UtcNow
        );

        // Act
        var formatted = snapshot.ToFormattedString();

        // Assert
        formatted.Should().Contain("[Exception] Metric: SuccessfulOp");
        formatted.Should().Contain($"Exceptions / Operations: 0 / 10 ({snapshot.ExceptionRate:P2})");
        formatted.Should().NotContain("Exceptions Breakdown:");
    }

    #endregion

    #region FailureSnapshot Tests

    [Fact]
    public void FailureSnapshot_ShouldImplementIMetricSnapshot_AndDistinguishFailureTypes()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshot = new FailureSnapshot(
            MetricName: "Checkout",
            CounterName: "Failure",
            TotalOperations: 100,
            TotalFailures: 15,
            LogicalFailures: 10,
            ExceptionFailures: 5,
            Timestamp: now
        );

        // Assert
        IMetricSnapshot metricSnapshot = snapshot;
        metricSnapshot.MetricName.Should().Be("Checkout");
        metricSnapshot.CounterName.Should().Be("Failure");

        snapshot.TotalOperations.Should().Be(100);
        snapshot.TotalFailures.Should().Be(15);
        snapshot.LogicalFailures.Should().Be(10);
        snapshot.ExceptionFailures.Should().Be(5);
        snapshot.FailureRate.Should().Be(0.15);
        snapshot.LogicalFailureRate.Should().Be(0.10);
        snapshot.ExceptionFailureRate.Should().Be(0.05);

        var formatted = snapshot.ToFormattedString();
        formatted.Should().Contain("[Failure] Metric: Checkout");
        formatted.Should().Contain($"Failures / Operations: 15 / 100 ({snapshot.FailureRate:P2})");
        formatted.Should().Contain($"Logical Failures   : 10 ({snapshot.LogicalFailureRate:P2})");
        formatted.Should().Contain($"Exception Failures : 5 ({snapshot.ExceptionFailureRate:P2})");
    }

    [Fact]
    public void FailureSnapshot_Rates_WhenTotalOperationsIsZero_ShouldReturnZero()
    {
        var snapshot = new FailureSnapshot("Op", "Failure", 0, 0, 0, 0, DateTime.UtcNow);
        snapshot.FailureRate.Should().Be(0.0);
        snapshot.LogicalFailureRate.Should().Be(0.0);
        snapshot.ExceptionFailureRate.Should().Be(0.0);
    }


    [Fact]
    public void ExceptionSnapshot_RecordEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var dict = new Dictionary<string, long> { { "Ex", 1 } };
        var snapshot1 = new ExceptionSnapshot("Op", "Exception", 5, 1, dict, timestamp);
        var snapshot2 = new ExceptionSnapshot("Op", "Exception", 5, 1, dict, timestamp);
        var snapshot3 = snapshot1 with { TotalExceptions = 2 };

        // Assert
        snapshot1.Should().Be(snapshot2);
        snapshot1.Should().NotBe(snapshot3);
    }

    #endregion

    #region ThroughputSnapshot Tests

    [Fact]
    public void ThroughputSnapshot_ShouldImplementIMetricSnapshot_AndExposeProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var duration = TimeSpan.FromSeconds(4);
        var snapshot = new ThroughputSnapshot(
            MetricName: "BatchIndexer",
            CounterName: "Throughput",
            TotalItems: 20000,
            TotalOperations: 100,
            TotalDuration: duration,
            ItemsPerSecond: 5000.0,
            AverageItemsPerOperation: 200.0,
            Timestamp: now,
            FailedOperations: 2
        );

        // Assert interface implementation
        IMetricSnapshot metricSnapshot = snapshot;
        metricSnapshot.MetricName.Should().Be("BatchIndexer");
        metricSnapshot.CounterName.Should().Be("Throughput");
        metricSnapshot.Timestamp.Should().Be(now);

        // Assert properties
        snapshot.TotalItems.Should().Be(20000);
        snapshot.TotalOperations.Should().Be(100);
        snapshot.TotalDuration.Should().Be(duration);
        snapshot.ItemsPerSecond.Should().Be(5000.0);
        snapshot.AverageItemsPerOperation.Should().Be(200.0);
        snapshot.FailedOperations.Should().Be(2);
    }

    [Fact]
    public void ThroughputSnapshot_RecordEquality_ShouldWorkAsExpected()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var snapshot1 = new ThroughputSnapshot("Op", "Throughput", 100, 2, TimeSpan.FromSeconds(1), 100, 50, timestamp);
        var snapshot2 = new ThroughputSnapshot("Op", "Throughput", 100, 2, TimeSpan.FromSeconds(1), 100, 50, timestamp);
        var different = new ThroughputSnapshot("Op", "Throughput", 200, 4, TimeSpan.FromSeconds(2), 100, 50, timestamp);

        // Assert
        snapshot1.Should().Be(snapshot2);
        snapshot1.GetHashCode().Should().Be(snapshot2.GetHashCode());
        snapshot1.Should().NotBe(different);
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

    #region Typed Snapshot Retrieval Tests

    [Fact]
    public void GetSnapshot_Generic_WithoutCounterName_ShouldResolveMatchingSnapshotType()
    {
        // Arrange
        var tracker = new MetricTracker("GenericSnapshotTopic")
            .AddThroughputCounter()
            .AddExceptionCounter()
            .AddMemoryCounter();

        tracker.In("ProcessPayment");
        tracker.Out("ProcessPayment", failed: true, exception: new InvalidOperationException("Test error"));

        // Act
        var duration = tracker.GetSnapshot<DurationSnapshot>("ProcessPayment");
        var throughput = tracker.GetSnapshot<ThroughputSnapshot>("ProcessPayment");
        var exception = tracker.GetSnapshot<ExceptionSnapshot>("ProcessPayment");
        var memory = tracker.GetSnapshot<MemorySnapshot>("ProcessPayment");

        // Assert
        duration.Should().NotBeNull();
        duration!.MetricName.Should().Be("ProcessPayment");
        duration.InCount.Should().Be(1);

        throughput.Should().NotBeNull();
        throughput!.MetricName.Should().Be("ProcessPayment");
        throughput.TotalOperations.Should().Be(1);
        throughput.FailedOperations.Should().Be(1);

        exception.Should().NotBeNull();
        exception!.MetricName.Should().Be("ProcessPayment");
        exception.TotalFailures.Should().Be(1);

        memory.Should().NotBeNull();
        memory!.MetricName.Should().Be("ProcessPayment");
    }

    [Fact]
    public void GetSnapshot_Generic_WithExplicitCounterName_ShouldResolveMatchingSnapshot()
    {
        // Arrange
        var tracker = new MetricTracker("GenericExplicitTopic")
            .AddThroughputCounter("CustomThroughput")
            .AddDimensionCounter("region");

        tracker.In("OrderFulfillment", new() { ["region"] = "EU" });
        tracker.Out("OrderFulfillment", new() { ["region"] = "EU" });

        // Act
        var throughput = tracker.GetSnapshot<ThroughputSnapshot>("OrderFulfillment", "CustomThroughput");
        var dimension = tracker.GetSnapshot<DimensionSnapshot>("OrderFulfillment", "Dimension:region");
        var wrongName = tracker.GetSnapshot<ThroughputSnapshot>("OrderFulfillment", "NonExistentCounter");

        // Assert
        throughput.Should().NotBeNull();
        throughput!.CounterName.Should().Be("CustomThroughput");

        dimension.Should().NotBeNull();
        dimension!.DimensionName.Should().Be("region");

        wrongName.Should().BeNull();
    }

    [Fact]
    public void GetSnapshot_Generic_WithMismatchedTypeOrMissingMetric_ShouldReturnNull()
    {
        // Arrange
        var tracker = new MetricTracker("MismatchTopic")
            .AddThroughputCounter();

        tracker.In("ValidOp");
        tracker.Out("ValidOp");

        // Act & Assert
        // Metric exists, but counter name points to Throughput, requested DurationSnapshot -> returns null
        tracker.GetSnapshot<DurationSnapshot>("ValidOp", ThroughputCounter.DefaultCounterName).Should().BeNull();

        // Metric does not exist
        tracker.GetSnapshot<ThroughputSnapshot>("NonExistentOp").Should().BeNull();
    }

    [Fact]
    public void GetSnapshots_Generic_ShouldFilterSnapshotsByRequestedType()
    {
        // Arrange
        var tracker = new MetricTracker("FilterTopic")
            .AddDimensionCounter("region")
            .AddDimensionCounter("env");

        tracker.In("Login", new() { ["region"] = "US", ["env"] = "Prod" });
        tracker.Out("Login", new() { ["region"] = "US", ["env"] = "Prod" });

        // Act
        var dimensionSnapshots = tracker.GetSnapshots<DimensionSnapshot>("Login").ToList();
        var durationSnapshots = tracker.GetSnapshots<DurationSnapshot>("Login").ToList();

        // Assert
        dimensionSnapshots.Should().HaveCount(2);
        durationSnapshots.Should().HaveCount(1);
    }

    [Fact]
    public void GetAllSnapshots_Generic_ShouldFilterAllSnapshotsByRequestedType()
    {
        // Arrange
        var tracker = new MetricTracker("AllFilterTopic")
            .AddExceptionCounter();

        tracker.In("Op1");
        tracker.Out("Op1", failed: true, exception: new Exception("err"));
        tracker.In("Op2");
        tracker.Out("Op2");

        // Act
        IMetricSnapshotsSource source = tracker;
        var exceptions = source.GetAllSnapshots<ExceptionSnapshot>().ToList();

        // Assert
        exceptions.Should().HaveCount(2);
        exceptions.Select(e => e.MetricName).Should().Contain(["Op1", "Op2"]);
    }

    [Fact]
    public void DedicatedHelperMethods_ShouldReturnExpectedTypedSnapshots()
    {
        // Arrange
        var tracker = new MetricTracker("HelpersTopic")
            .AddThroughputCounter()
            .AddExceptionCounter()
            .AddFailureCounter()
            .AddMemoryCounter()
            .AddDimensionCounter("tenant");

        tracker.In("Checkout", new() { ["tenant"] = "Acme" });
        tracker.Out("Checkout", new() { ["tenant"] = "Acme" }, failed: true, exception: new InvalidOperationException());

        // Act - Dedicated helper methods from Solution 1.A (using Solution 2.A internally)
        var duration = TrackerCounterExtensions.GetDurationSnapshot(tracker, "Checkout");
        var throughput = tracker.GetThroughputSnapshot("Checkout");
        var exception = tracker.GetExceptionSnapshot("Checkout");
        var failure = tracker.GetFailureShapshot("Checkout");
        var memory = tracker.GetMemorySnapshot("Checkout");
        var dimension = tracker.GetDimensionSnapshot("Checkout", "tenant");

        // Assert
        duration.Should().NotBeNull();
        duration!.MetricName.Should().Be("Checkout");

        throughput.Should().NotBeNull();
        throughput!.MetricName.Should().Be("Checkout");

        exception.Should().NotBeNull();
        exception!.MetricName.Should().Be("Checkout");

        failure.Should().NotBeNull();
        failure!.MetricName.Should().Be("Checkout");
        failure.TotalFailures.Should().Be(1);
        failure.ExceptionFailures.Should().Be(1);

        memory.Should().NotBeNull();
        memory!.MetricName.Should().Be("Checkout");

        dimension.Should().NotBeNull();
        dimension!.DimensionName.Should().Be("tenant");
    }

    [Fact]
    public void GetDurationSnapshot_OnIMetricTracker_ShouldResolveDurationSnapshot()
    {
        // Arrange
        IMetricTracker tracker = new MetricTracker("InterfaceTopic");
        tracker.In("Action");
        tracker.Out("Action");

        // Act - Calls extension method on IMetricTracker
        var snapshot = tracker.GetDurationSnapshot("Action");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.MetricName.Should().Be("Action");
        snapshot.InCount.Should().Be(1);
    }

    #endregion
}
