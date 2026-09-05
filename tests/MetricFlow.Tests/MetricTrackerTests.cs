using DotnetKit.MetricFlow.Tracker;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests
{
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

    }
}