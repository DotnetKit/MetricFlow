using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class TrackerActionExtensionsTests
{
    [Fact]
    public void TrackAction_ShouldExecuteAndRecordSuccess()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic").AddExceptionCounter();
        var executed = false;

        // Act
        tracker.TrackAction("SyncOp", () => { executed = true; });

        // Assert
        executed.Should().BeTrue();
        var snapshot = tracker.GetValues("SyncOp");
        snapshot.Should().NotBeNull();
        snapshot!.InCount.Should().Be(1);
        snapshot.OutCount.Should().Be(1);
        snapshot.FailedCount.Should().Be(0);
    }

    [Fact]
    public void TrackAction_WhenExceptionThrown_ShouldRecordExceptionAndRethrow()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic").AddExceptionCounter();

        // Act
        var act = () => tracker.TrackAction("FailingOp", () =>
        {
            throw new InvalidOperationException("Sync error");
        });

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Sync error");
        var snapshot = tracker.GetValues("FailingOp");
        snapshot.Should().NotBeNull();
        snapshot!.FailedCount.Should().Be(1);

        var excSnapshot = tracker.GetSnapshot("FailingOp", "Exception") as ExceptionSnapshot;
        excSnapshot.Should().NotBeNull();
        excSnapshot!.ExceptionsByType.Should().ContainKey(nameof(InvalidOperationException));
    }

    [Fact]
    public void TrackAction_WithReturnValue_ShouldReturnResult()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic");

        // Act
        var result = tracker.TrackAction("ReturnOp", () => 42);

        // Assert
        result.Should().Be(42);
        var snapshot = tracker.GetValues("ReturnOp");
        snapshot.Should().NotBeNull();
        snapshot!.OutCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackActionAsync_ShouldExecuteAndRecordSuccess()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic").AddExceptionCounter();
        var executed = false;

        // Act
        await tracker.TrackActionAsync("AsyncOp", async () =>
        {
            await Task.Delay(5);
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
        var snapshot = tracker.GetValues("AsyncOp");
        snapshot.Should().NotBeNull();
        snapshot!.InCount.Should().Be(1);
        snapshot.OutCount.Should().Be(1);
        snapshot.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task TrackActionAsync_WhenExceptionThrown_ShouldRecordExceptionAndRethrow()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic").AddExceptionCounter();

        // Act
        var act = async () => await tracker.TrackActionAsync("FailingAsyncOp", async () =>
        {
            await Task.Delay(5);
            throw new TimeoutException("Async timeout");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("Async timeout");
        var snapshot = tracker.GetValues("FailingAsyncOp");
        snapshot.Should().NotBeNull();
        snapshot!.FailedCount.Should().Be(1);

        var excSnapshot = tracker.GetSnapshot("FailingAsyncOp", "Exception") as ExceptionSnapshot;
        excSnapshot.Should().NotBeNull();
        excSnapshot!.ExceptionsByType.Should().ContainKey(nameof(TimeoutException));
    }

    [Fact]
    public async Task TrackActionAsync_WithReturnValue_ShouldReturnResult()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic");

        // Act
        var result = await tracker.TrackActionAsync("AsyncReturnOp", async () =>
        {
            await Task.Delay(5);
            return "hello world";
        });

        // Assert
        result.Should().Be("hello world");
        var snapshot = tracker.GetValues("AsyncReturnOp");
        snapshot.Should().NotBeNull();
        snapshot!.OutCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackAction_WithCallerMemberName_ShouldInferMethodName()
    {
        // Arrange
        var tracker = new MetricTracker("ActionTopic");

        // Act
        HelperCallingTrackAction(tracker);
        await HelperCallingTrackActionAsync(tracker);

        // Assert
        tracker.GetValues(nameof(HelperCallingTrackAction)).Should().NotBeNull();
        tracker.GetValues(nameof(HelperCallingTrackActionAsync)).Should().NotBeNull();
    }

    private static void HelperCallingTrackAction(MetricTracker tracker)
    {
        tracker.TrackAction(() => { });
    }

    private static async Task HelperCallingTrackActionAsync(MetricTracker tracker)
    {
        await tracker.TrackActionAsync(async () => { await Task.Yield(); });
    }
}
