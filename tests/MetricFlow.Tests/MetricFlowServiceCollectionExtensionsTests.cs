using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MetricFlow.Tests;

public class MetricFlowServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMetricFlow_WithDefaults_RegistersExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow();
        using var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<MetricFlowOptions>();
        options.Should().NotBeNull();
        options.Topic.Should().Be("Application");
        options.SamplingRate.Should().Be(1.0);
        options.TopicTags.Should().BeNull();
        options.AutoAddExceptionCounter.Should().BeTrue();

        var tracker = provider.GetService<MetricTracker>();
        tracker.Should().NotBeNull();
        tracker.Topic.Should().Be("Application");

        var interfaceTracker = provider.GetService<IMetricTracker>();
        interfaceTracker.Should().NotBeNull();
        interfaceTracker.Should().BeSameAs(tracker);

        var snapshotSource = provider.GetService<IMetricSnapshotsSource>();
        snapshotSource.Should().NotBeNull();
        snapshotSource.Should().BeSameAs(tracker);

        // Exception counter should be automatically registered by default
        tracker.TrackAction("test_op", () => { });
        var snapshot = tracker.GetSnapshot("test_op", ExceptionCounter.DefaultCounterName);
        snapshot.Should().NotBeNull();
    }

    [Fact]
    public void AddMetricFlow_WithTopic_SetsCustomTopic()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow("OrderProcessorDaemon");
        using var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<MetricTracker>();
        tracker.Topic.Should().Be("OrderProcessorDaemon");

        var options = provider.GetRequiredService<MetricFlowOptions>();
        options.Topic.Should().Be("OrderProcessorDaemon");
    }

    [Fact]
    public void AddMetricFlow_WithTopicAndOptions_AppliesBoth()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow("PaymentWorker", options =>
        {
            options.SamplingRate = 0.5;
            options.TopicTags = new Dictionary<string, string>
            {
                ["env"] = "production",
                ["region"] = "us-east"
            };
            options.AutoAddExceptionCounter = false;
        });

        using var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<MetricTracker>();
        tracker.Topic.Should().Be("PaymentWorker");
        tracker.TopicTags.Should().ContainKey("env").WhoseValue.Should().Be("production");
        tracker.TopicTags.Should().ContainKey("region").WhoseValue.Should().Be("us-east");

        var options = provider.GetRequiredService<MetricFlowOptions>();
        options.SamplingRate.Should().Be(0.5);
        options.AutoAddExceptionCounter.Should().BeFalse();

        // Exception counter should NOT be registered when AutoAddExceptionCounter is false
        tracker.TrackAction("test_op", () => { });
        var snapshot = tracker.GetSnapshot("test_op", ExceptionCounter.DefaultCounterName);
        snapshot.Should().BeNull();
    }

    [Fact]
    public void AddMetricFlow_WithOptions_ConfiguresAdditionalCounters()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow(options =>
        {
            options.Topic = "QueueConsumer";
            options.Counters.Add(new ThroughputCounter("QueueThroughput"));
            options.Counters.Add(new MemoryCounter());
        });

        using var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<MetricTracker>();
        tracker.TrackAction("dequeue", () => { });

        var throughputSnapshot = tracker.GetSnapshot("dequeue", "QueueThroughput");
        throughputSnapshot.Should().NotBeNull();

        var memorySnapshot = tracker.GetSnapshot("dequeue", MemoryCounter.DefaultCounterName);
        memorySnapshot.Should().NotBeNull();
    }

    [Fact]
    public void AddMetricFlow_MultipleCalls_PreservesFirstRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow("FirstTopic");
        services.AddMetricFlow("SecondTopic");

        using var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<MetricTracker>();
        tracker.Topic.Should().Be("FirstTopic");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMetricFlow_InvalidTopic_ThrowsArgumentException(string? invalidTopic)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act1 = () => services.AddMetricFlow(invalidTopic!);
        var act2 = () => services.AddMetricFlow(invalidTopic!, _ => { });

        // Assert
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMetricFlow_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        var act1 = () => services.AddMetricFlow();
        var act2 = () => services.AddMetricFlow("Topic");
        var act3 = () => services.AddMetricFlow(_ => { });
        var act4 = () => services.AddMetricFlow("Topic", _ => { });

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
        act4.Should().Throw<ArgumentNullException>();
    }
}
