using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class ThroughputCounterTests
{
    [Fact]
    public void SingleOperation_WithoutItemTags_ShouldDefaultToOneItem()
    {
        // Arrange
        var tracker = new MetricTracker("ThroughputTest")
            .AddThroughputCounter();

        // Act
        using (tracker.Track("SingleItemOp"))
        {
            Thread.Sleep(5);
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("SingleItemOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TotalItems.Should().Be(1);
        snapshot.AverageItemsPerOperation.Should().Be(1.0);
        snapshot.TotalDuration.TotalMilliseconds.Should().BeGreaterThan(0);
        snapshot.ItemsPerSecond.Should().BeGreaterThan(0);
        snapshot.FailedOperations.Should().Be(0);
    }

    [Fact]
    public void BatchOperation_WithItemsTag_ShouldCountItemsAndCalculateRate()
    {
        // Arrange
        var tracker = new MetricTracker("ThroughputTest")
            .AddThroughputCounter();

        // Act
        using (tracker.Track("BatchOp", new() { ["items"] = "100" }))
        {
            Thread.Sleep(10);
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("BatchOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TotalItems.Should().Be(100);
        snapshot.AverageItemsPerOperation.Should().Be(100.0);
        snapshot.ItemsPerSecond.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("items", "50", 50)]
    [InlineData("count", "75", 75)]
    [InlineData("batch_size", "120", 120)]
    [InlineData("ITEMS", "60", 60)]
    [InlineData("BATCH_SIZE", "200", 200)]
    public void BatchOperation_WithAlternativeTagKeys_ShouldParseCorrectly(string tagKey, string tagValue, long expectedCount)
    {
        // Arrange
        var tracker = new MetricTracker("TagParsingTest")
            .AddThroughputCounter();

        // Act
        using (tracker.Track("AlternativeTagsOp", new() { [tagKey] = tagValue }))
        {
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("AlternativeTagsOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalItems.Should().Be(expectedCount);
        snapshot.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void TrackItems_ExtensionMethod_ShouldRecordItemCount()
    {
        // Arrange
        var tracker = new MetricTracker("TrackItemsTest")
            .AddThroughputCounter();

        // Act
        using (tracker.TrackItems("IngestChannels", 250, new() { ["source"] = "xml" }))
        {
            Thread.Sleep(5);
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("IngestChannels");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TotalItems.Should().Be(250);
        snapshot.AverageItemsPerOperation.Should().Be(250.0);
    }

    [Fact]
    public void TrackItems_WithCallerMemberName_ShouldInferMetricName()
    {
        // Arrange
        var tracker = new MetricTracker("CallerMemberTest")
            .AddThroughputCounter();

        // Act
        HelperTrackItemsCaller(tracker, 150);

        // Assert
        var snapshot = tracker.GetThroughputValues(nameof(HelperTrackItemsCaller));
        snapshot.Should().NotBeNull();
        snapshot.TotalItems.Should().Be(150);
        snapshot.TotalOperations.Should().Be(1);
    }

    private static void HelperTrackItemsCaller(MetricTracker tracker, long count)
    {
        using (tracker.TrackItems(count))
        {
        }
    }

    [Fact]
    public void Scope_SetItems_ShouldAllowDynamicBatchSizing()
    {
        // Arrange
        var tracker = new MetricTracker("DynamicBatchTest")
            .AddThroughputCounter();

        // Act - Item count is determined during operation execution
        using (var scope = tracker.Track("DynamicBatchOp"))
        {
            // Simulate processing
            long processedRecords = 42;
            scope.SetItems(processedRecords);
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("DynamicBatchOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TotalItems.Should().Be(42);
        snapshot.AverageItemsPerOperation.Should().Be(42.0);
    }

    [Fact]
    public void Scope_SetTag_ShouldRecognizeBatchSizeTag()
    {
        // Arrange
        var tracker = new MetricTracker("SetTagTest")
            .AddThroughputCounter();

        // Act
        using (var scope = tracker.Track("TagBatchOp"))
        {
            scope.SetTag("batch_size", "88");
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("TagBatchOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalItems.Should().Be(88);
    }

    [Fact]
    public void MultipleOperations_ShouldAggregateTotalsAndAverages()
    {
        // Arrange
        var tracker = new MetricTracker("MultiOpTest")
            .AddThroughputCounter();

        // Act
        using (tracker.TrackItems("MultiBatch", 100)) { }
        using (tracker.TrackItems("MultiBatch", 300)) { }
        using (tracker.TrackItems("MultiBatch", 200)) { }

        // Assert
        var snapshot = tracker.GetThroughputValues("MultiBatch");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(3);
        snapshot.TotalItems.Should().Be(600);
        snapshot.AverageItemsPerOperation.Should().Be(200.0);
    }

    [Fact]
    public void FailedOperation_ShouldTrackFailureCount()
    {
        // Arrange
        var tracker = new MetricTracker("FailedOpTest")
            .AddThroughputCounter();

        // Act - 1 success, 1 failure via scope.SetFailed, 1 failure via exception
        using (tracker.TrackItems("FailTest", 50)) { }

        using (var scope = tracker.TrackItems("FailTest", 25))
        {
            scope.SetFailed(true);
        }

        try
        {
            using (var scope = tracker.TrackItems("FailTest", 25))
            {
                try
                {
                    throw new ApplicationException("Simulated error");
                }
                catch (Exception ex)
                {
                    scope.SetException(ex);
                    throw;
                }
            }
        }
        catch (ApplicationException)
        {
            // Expected
        }

        // Assert
        var snapshot = tracker.GetThroughputValues("FailTest");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(3);
        snapshot.TotalItems.Should().Be(100);
        snapshot.FailedOperations.Should().Be(2);
    }

    [Fact]
    public void ManualInOut_WithItemsTag_ShouldRecordThroughput()
    {
        // Arrange
        var tracker = new MetricTracker("ManualInOutTest")
            .AddThroughputCounter();

        // Act
        tracker.In("ManualOp");
        Thread.Sleep(5);
        tracker.Out("ManualOp", tags: new() { ["items"] = "500" });

        // Assert
        var snapshot = tracker.GetThroughputValues("ManualOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TotalItems.Should().Be(500);
        snapshot.TotalDuration.TotalMilliseconds.Should().BeGreaterThan(0);
        snapshot.ItemsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConcurrentExecution_ShouldBeThreadSafeAndAccurate()
    {
        // Arrange
        var tracker = new MetricTracker("ConcurrencyTest")
            .AddThroughputCounter();

        const int threads = 10;
        const int opsPerThread = 50;
        const int itemsPerOp = 20;

        // Act
        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                using (tracker.TrackItems("ConcurrentMetric", itemsPerOp))
                {
                }
            }
        });

        // Assert
        var snapshot = tracker.GetThroughputValues("ConcurrentMetric");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(threads * opsPerThread);
        snapshot.TotalItems.Should().Be((long)threads * opsPerThread * itemsPerOp);
        snapshot.AverageItemsPerOperation.Should().Be(itemsPerOp);
    }

    [Fact]
    public void Reset_ShouldClearAllThroughputStates()
    {
        // Arrange
        var tracker = new MetricTracker("ResetTest")
            .AddThroughputCounter();

        using (tracker.TrackItems("OpToReset", 50)) { }
        tracker.GetThroughputValues("OpToReset").Should().NotBeNull();

        // Act
        tracker.Clear();

        // Assert
        tracker.GetThroughputValues("OpToReset").Should().BeNull();
        tracker.GetAllSnapshots().Should().BeEmpty();
    }

    [Fact]
    public void DisabledCounter_ShouldNotTrackItemsOrOperations()
    {
        // Arrange
        var tracker = new MetricTracker("DisabledTest")
            .AddThroughputCounter();

        tracker.SetCounterEnabled(ThroughputCounter.DefaultCounterName, false);

        // Act
        using (tracker.TrackItems("DisabledMetric", 100)) { }

        // Assert
        tracker.GetThroughputValues("DisabledMetric").Should().BeNull();
    }

    [Fact]
    public void ItemCounter_ShouldWorkAsAliasWithDefaultNameItem()
    {
        // Arrange
        var tracker = new MetricTracker("ItemCounterTest")
            .AddItemCounter();

        // Act
        using (tracker.TrackItems("ItemOp", 77)) { }

        // Assert
        var snapshot = tracker.GetSnapshot("ItemOp", ItemCounter.DefaultCounterName) as ThroughputSnapshot;
        snapshot.Should().NotBeNull();
        snapshot.CounterName.Should().Be("Item");
        snapshot.TotalItems.Should().Be(77);
        snapshot.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void ThroughputSnapshot_ToFormattedString_ShouldIncludeAllExpectedMetrics()
    {
        // Arrange
        var snapshot = new ThroughputSnapshot(
            MetricName: "ProcessBatch",
            CounterName: "Throughput",
            TotalItems: 10000,
            TotalOperations: 50,
            TotalDuration: TimeSpan.FromSeconds(2),
            ItemsPerSecond: 5000.0,
            AverageItemsPerOperation: 200.0,
            Timestamp: DateTime.UtcNow,
            FailedOperations: 1
        );

        // Act
        var formatted = snapshot.ToFormattedString();

        // Assert
        formatted.Should().Contain("[Throughput] Metric: ProcessBatch");
        formatted.Should().Contain($"Total Items Processed : {snapshot.TotalItems:N0}");
        formatted.Should().Contain($"Batch Operations       : {snapshot.TotalOperations:N0} (avg {snapshot.AverageItemsPerOperation:N1} items/op)");
        formatted.Should().Contain($"Throughput             : {snapshot.ItemsPerSecond:N0} items/sec");
        snapshot.ToString().Should().Be(formatted);
    }

    [Fact]
    public void InterfaceExtensions_ShouldWorkOnIMetricTracker()
    {
        // Arrange
        IMetricTracker tracker = new MetricTracker("InterfaceTest")
            .AddThroughputCounter();

        // Act
        using (tracker.TrackItems("InterfaceOp", 123)) { }

        // Assert
        var snapshot = tracker.GetThroughputValues("InterfaceOp");
        snapshot.Should().NotBeNull();
        snapshot.TotalItems.Should().Be(123);
        snapshot.TotalOperations.Should().Be(1);
    }
}
