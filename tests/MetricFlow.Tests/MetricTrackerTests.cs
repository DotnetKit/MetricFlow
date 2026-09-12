using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Extensions;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class MetricTrackerTests
{
    [Fact]
    public void MetricTracker_ShouldInitializeWithTopicAndTags()
    {
        // Arrange
        var topic = "TestTopic";
        var tags = new Dictionary<string, string> { { "tag1", "value1" } };

        // Act
        var tracker = new MetricTracker(topic, tags);

        // Assert
        tracker.Topic.Should().Be(topic);
        tracker.TopicTags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void MetricTracker_ShouldTrackInAndOut()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");

        // Act
        tracker.In("TestMetric");
        tracker.Out("TestMetric");

        // Assert
        var values = tracker.GetValues("TestMetric");
        values.Should().NotBeNull();
        values!.InCount.Should().Be(1);
        values.OutCount.Should().Be(1);
    }


    [Fact]
    public void MetricTracker_ShouldRespectSamplingRate()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic", samplingRate: 0.0); // 0% sampling rate

        // Act
        tracker.In("TestMetric");
        tracker.Out("TestMetric");

        // Assert
        var values = tracker.GetValues("TestMetric");
        values.Should().BeNull(); // No metrics should be tracked
    }

    [Fact]
    public async Task MetricTracker_ShouldTrackUsingDisposablePattern()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");

        // Act
        using (var _ = tracker.Track("TestMetric"))
        {
            await Task.Delay(10);
        }

        // Assert
        var values = tracker.GetValues("TestMetric");
        values.Should().NotBeNull();
        values?.InCount.Should().Be(1);
        values?.OutCount.Should().Be(1);
    }
    [Fact]
    public void MetricTracker_ShouldTrackFailedMetrics()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");

        // Act
        tracker.In("TestMetric");
        tracker.Out("TestMetric");

        tracker.In("TestMetric");
        tracker.Out("TestMetric", failed: true);

        // Assert
        var values = tracker.GetValues("TestMetric");
        values.Should().NotBeNull();
        values!.InCount.Should().Be(2);
        values.OutCount.Should().Be(2);
        values.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task MetricTracker_ShouldRecordAccurateDuration()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");

        // Act
        using (tracker.Track("DelayedMetric"))
        {
            await Task.Delay(50);
        }

        // Assert
        var values = tracker.GetValues("DelayedMetric");
        values.Should().NotBeNull();
        values!.TotalDuration.TotalMilliseconds.Should().BeInRange(30, 500);
        values.AverageDuration.TotalMilliseconds.Should().BeInRange(30, 500);
    }

    [Fact]
    public async Task MetricTracker_ShouldHandleConcurrentTracking()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");
        const int concurrency = 20;

        // Act
        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            using (tracker.Track("ConcurrentMetric"))
            {
                await Task.Delay(10);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var values = tracker.GetValues("ConcurrentMetric");
        values.Should().NotBeNull();
        values!.InCount.Should().Be(concurrency);
        values.OutCount.Should().Be(concurrency);
        values.AverageDuration.TotalMilliseconds.Should().BeInRange(5, 500);
    }

    [Fact]
    public void MetricTracker_SamplingRateNull_ShouldTrackAll()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic", samplingRate: null);

        // Act
        tracker.In("TestMetric");
        tracker.Out("TestMetric");

        // Assert
        var values = tracker.GetValues("TestMetric");
        values.Should().NotBeNull();
        values!.InCount.Should().Be(1);
        values.OutCount.Should().Be(1);
    }

    [Fact]
    public void Track_WithSetFailed_ShouldRecordFailureWithoutException()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic")
            .AddExceptionCounter();

        // Act
        using (var scope = tracker.Track("LogicalFailureOp"))
        {
            // Simulate a logical failure (e.g. Result.Failure or HTTP 400 without exception)
            scope.SetFailed();
        }

        // Assert DurationCounter records failed: true
        var durationValues = tracker.GetValues("LogicalFailureOp");
        durationValues.Should().NotBeNull();
        durationValues!.InCount.Should().Be(1);
        durationValues.OutCount.Should().Be(1);
        durationValues.FailedCount.Should().Be(1);

        // Assert ExceptionCounter records failure with "UnspecifiedError" category
        var exceptionSnapshot = tracker.GetSnapshot("LogicalFailureOp", ExceptionCounter.DefaultCounterName) as ExceptionSnapshot;
        exceptionSnapshot.Should().NotBeNull();
        exceptionSnapshot!.TotalOperations.Should().Be(1);
        exceptionSnapshot.TotalFailures.Should().Be(1);
        exceptionSnapshot.ExceptionsByType.Should().ContainKey("UnspecifiedError").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void Track_WithSetFailedFalse_ShouldNotRecordFailure()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");

        // Act
        using (var scope = tracker.Track("SuccessfulOp"))
        {
            scope.SetFailed(false);
        }

        // Assert
        var values = tracker.GetValues("SuccessfulOp");
        values.Should().NotBeNull();
        values!.InCount.Should().Be(1);
        values.OutCount.Should().Be(1);
        values.FailedCount.Should().Be(0);
    }

    [Fact]
    public void Track_WhenDroppedBySampling_SetFailedShouldBeSafeNoOp()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic", samplingRate: 0.0);

        // Act & Assert (should not throw InvalidCastException or NullReferenceException)
        var act = () =>
        {
            using var scope = tracker.Track("DroppedOp");
            scope.SetFailed();
        };

        act.Should().NotThrow();
    }
 
    [Fact]
    public void Tracker_ShouldImplementIMetricSnapshotsSource()
    {
        // Arrange
        var tracker = new MetricTracker("SourceTopic");
        tracker.In("SourceOp");
        tracker.Out("SourceOp");

        // Act
        IMetricSnapshotsSource source = tracker;
        var snapshots = source.GetAllSnapshots();
        var formatted = source.ToFormattedString();

        // Assert
        snapshots.Should().ContainSingle(s => s.MetricName == "SourceOp");
        formatted.Should().Contain("Metric: SourceOp");
        formatted.Should().Contain("Duration");
    }

    [Fact]
    public void Track_WithNoMetricName_ShouldResolveCallingMethodName()
    {
        // Arrange
        var tracker = new MetricTracker("CallerMemberTopic");

        // Act
        HelperCallingMethodForTrack(tracker);

        // Assert
        var snapshot = tracker.GetValues(nameof(HelperCallingMethodForTrack));
        snapshot.Should().NotBeNull();
        snapshot!.InCount.Should().Be(1);
        snapshot.OutCount.Should().Be(1);
    }

    private static void HelperCallingMethodForTrack(MetricTracker tracker)
    {
        using (tracker.Track())
        {
        }
    }

    [Fact]
    public void Track_WithTagsOnly_ShouldResolveCallingMethodName()
    {
        // Arrange
        var tracker = new MetricTracker("CallerMemberTopic");
        var tags = new Dictionary<string, string> { ["env"] = "test" };

        // Act
        HelperCallingMethodWithTags(tracker, tags);

        // Assert
        var snapshot = tracker.GetValues(nameof(HelperCallingMethodWithTags));
        snapshot.Should().NotBeNull();
        snapshot!.InCount.Should().Be(1);
        snapshot.OutCount.Should().Be(1);
    }

    private static void HelperCallingMethodWithTags(MetricTracker tracker, Dictionary<string, string> tags)
    {
        using (tracker.Track(tags))
        {
        }
    }

    [Fact]
    public void InAndOut_WithNoMetricName_ShouldResolveCallingMethodName()
    {
        // Arrange
        var tracker = new MetricTracker("CallerMemberTopic");

        // Act
        HelperCallingMethodForInOut(tracker);

        // Assert
        var snapshot = tracker.GetValues(nameof(HelperCallingMethodForInOut));
        snapshot.Should().NotBeNull();
        snapshot!.InCount.Should().Be(1);
        snapshot.OutCount.Should().Be(1);
    }

    private static void HelperCallingMethodForInOut(MetricTracker tracker)
    {
        tracker.In();
        tracker.Out();
    }
}