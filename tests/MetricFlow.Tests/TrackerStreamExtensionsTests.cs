using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class TrackerStreamExtensionsTests
{
    [Fact]
    public async Task TrackStream_AsyncEnumerable_RecordsItemsAndDuration()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter());

        static async IAsyncEnumerable<int> GenerateNumbersAsync()
        {
            for (int i = 1; i <= 5; i++)
            {
                await Task.Delay(5);
                yield return i;
            }
        }

        // Act
        var result = new List<int>();
        await foreach (var item in GenerateNumbersAsync().TrackStream(tracker, "GetNumbers"))
        {
            result.Add(item);
        }

        // Assert
        result.Should().Equal(1, 2, 3, 4, 5);

        var durationSnap = tracker.GetDurationSnapshot("GetNumbers");
        durationSnap.Should().NotBeNull();
        durationSnap.InCount.Should().Be(1);
        durationSnap.OutCount.Should().Be(1);
        durationSnap.FailedCount.Should().Be(0);
        durationSnap.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);

        var throughputSnap = tracker.GetThroughputSnapshot("GetNumbers");
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(5);
        throughputSnap.TotalOperations.Should().Be(1);
        throughputSnap.FailedOperations.Should().Be(0);
    }

    [Fact]
    public async Task TrackStream_AsyncEnumerable_WhenExceptionThrown_RecordsFailureAndException()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter().AddFailureCounter());

        static async IAsyncEnumerable<string> FailingStreamAsync()
        {
            yield return "item1";
            yield return "item2";
            await Task.Delay(5);
            throw new InvalidOperationException("Stream connection dropped");
        }

        // Act
        var result = new List<string>();
        var act = async () =>
        {
            await foreach (var item in FailingStreamAsync().TrackStream(tracker, "FailingOp"))
            {
                result.Add(item);
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stream connection dropped");

        result.Should().Equal("item1", "item2");

        var durationSnap = tracker.GetDurationSnapshot("FailingOp");
        durationSnap.Should().NotBeNull();
        durationSnap.OutCount.Should().Be(1);
        durationSnap.FailedCount.Should().Be(1);

        var throughputSnap = tracker.GetThroughputSnapshot("FailingOp");
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(2); // Recorded the 2 items yielded before exception
        throughputSnap.FailedOperations.Should().Be(1);

        var exSnap = tracker.GetExceptionSnapshot("FailingOp");
        exSnap.Should().NotBeNull();
        exSnap.TotalExceptions.Should().Be(1);
        exSnap.ExceptionsByType.Should().ContainKey("InvalidOperationException");
    }

    [Fact]
    public async Task TrackStream_AsyncEnumerable_EarlyTermination_TracksConsumedItems()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter());

        static async IAsyncEnumerable<int> StreamWithManyItemsAsync()
        {
            int i = 0;
            while (i < 100)
            {
                yield return ++i;
            }
        }

        // Act
        var result = new List<int>();
        await foreach (var item in StreamWithManyItemsAsync().TrackStream(tracker, "Take3"))
        {
            result.Add(item);
            if (result.Count == 3)
            {
                break;
            }
        }

        // Assert
        result.Should().Equal(1, 2, 3);

        var throughputSnap = tracker.GetThroughputSnapshot("Take3");
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(3);
        throughputSnap.TotalOperations.Should().Be(1);
    }

    [Fact]
    public async Task TrackStream_AsyncEnumerable_WithCallerMemberName_UsesCallingMethod()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter());

        // Act
        await ExecuteStreamWithCallerMemberNameAsync(tracker);

        // Assert
        var durationSnap = tracker.GetDurationSnapshot(nameof(ExecuteStreamWithCallerMemberNameAsync));
        durationSnap.Should().NotBeNull();
        durationSnap.OutCount.Should().Be(1);

        var throughputSnap = tracker.GetThroughputSnapshot(nameof(ExecuteStreamWithCallerMemberNameAsync));
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(3);
    }

    private static async Task ExecuteStreamWithCallerMemberNameAsync(IMetricTracker tracker)
    {
        static async IAsyncEnumerable<int> LocalStream()
        {
            yield return 10;
            yield return 20;
            yield return 30;
        }

        await foreach (var _ in LocalStream().TrackStream(tracker))
        {
        }
    }

    [Fact]
    public void TrackStream_SyncEnumerable_RecordsItemsAndDuration()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter());

        var items = new[] { "apple", "banana", "cherry", "date" };

        // Act
        var result = items.TrackStream(tracker, "ProcessFruits").ToList();

        // Assert
        result.Should().Equal("apple", "banana", "cherry", "date");

        var durationSnap = tracker.GetDurationSnapshot("ProcessFruits");
        durationSnap.Should().NotBeNull();
        durationSnap.OutCount.Should().Be(1);
        durationSnap.FailedCount.Should().Be(0);

        var throughputSnap = tracker.GetThroughputSnapshot("ProcessFruits");
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(4);
    }

    [Fact]
    public void TrackStream_SyncEnumerable_WhenExceptionThrown_RecordsFailure()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddThroughputCounter());

        static IEnumerable<int> FailingSequence()
        {
            yield return 1;
            yield return 2;
            throw new InvalidOperationException("Simulated error");
        }

        // Act
        var act = () => FailingSequence().TrackStream(tracker, "SyncFailing").ToList();

        // Assert
        act.Should().Throw<InvalidOperationException>();

        var durationSnap = tracker.GetDurationSnapshot("SyncFailing");
        durationSnap.Should().NotBeNull();
        durationSnap.FailedCount.Should().Be(1);

        var throughputSnap = tracker.GetThroughputSnapshot("SyncFailing");
        throughputSnap.Should().NotBeNull();
        throughputSnap.TotalItems.Should().Be(2);
        throughputSnap.FailedOperations.Should().Be(1);
    }

    [Fact]
    public void TrackStream_ArgumentValidation_ThrowsExpectedExceptions()
    {
        var tracker = new MetricTracker("Test");
        IAsyncEnumerable<int>? nullAsyncStream = null;
        IEnumerable<int>? nullSyncStream = null;

        var act1 = () => nullAsyncStream!.TrackStream(tracker, "Op");
        var act2 = () => AsyncEnumerable.Empty<int>().TrackStream(null!, "Op");
        var act3 = () => AsyncEnumerable.Empty<int>().TrackStream(tracker, "   ");

        var act4 = () => nullSyncStream!.TrackStream(tracker, "Op");
        var act5 = () => Enumerable.Empty<int>().TrackStream(null!, "Op");
        var act6 = () => Enumerable.Empty<int>().TrackStream(tracker, "");

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentException>();

        act4.Should().Throw<ArgumentNullException>();
        act5.Should().Throw<ArgumentNullException>();
        act6.Should().Throw<ArgumentException>();
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.Yield();
            yield break;
        }
    }
}
