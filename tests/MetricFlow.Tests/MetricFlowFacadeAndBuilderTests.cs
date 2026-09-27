using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MetricFlow.Tests;

public class MetricFlowFacadeAndBuilderTests
{
    [Fact]
    public void FluentBuilder_AddTrackers_RegistersMultipleTopicsAndKeyedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Using fluent builder based on IFluentBuilder
        services.AddMetricFlow(opt => opt.SamplingRate = 0.8)
            .AddMetricTracker("WebApiExample", opt => opt.SamplingRate = 1.0)
            .AddMetricTracker("MyPlixConsole", opt =>
            {
                opt.SamplingRate = 0.5;
                opt.AddThroughputCounter();
            });

        using var provider = services.BuildServiceProvider();

        // Assert - Top level facade IMetricFlow
        var metricFlow = provider.GetRequiredService<IMetricFlow>();
        metricFlow.Should().NotBeNull();

        var webApiTracker = metricFlow.GetTracker("WebApiExample");
        webApiTracker.Should().NotBeNull();
        webApiTracker.Topic.Should().Be("WebApiExample");

        var consoleTracker = metricFlow.GetTracker("MyPlixConsole");
        consoleTracker.Should().NotBeNull();
        consoleTracker.Topic.Should().Be("MyPlixConsole");
        consoleTracker.GetCounters().Should().Contain(c => c.Name == "Throughput");

        // Assert - Default tracker is the first registered tracker
        metricFlow.DefaultTracker.Should().NotBeNull();
        metricFlow.DefaultTracker!.Topic.Should().Be("WebApiExample");

        // Assert - Indexer syntax
        metricFlow["MyPlixConsole"].Should().BeSameAs(consoleTracker);

        // Assert - Trackers enumeration
        metricFlow.Trackers.Should().HaveCount(2);

        // Assert - Native Keyed Injection (.NET 8+)
        var keyedWebApi = provider.GetRequiredKeyedService<IMetricTracker>("WebApiExample");
        keyedWebApi.Should().BeSameAs(webApiTracker);

        var keyedConsole = provider.GetRequiredKeyedService<IMetricTracker>("MyPlixConsole");
        keyedConsole.Should().BeSameAs(consoleTracker);

        // Assert - Default un-keyed IMetricTracker resolves to DefaultTracker
        var defaultTracker = provider.GetRequiredService<IMetricTracker>();
        defaultTracker.Should().BeSameAs(webApiTracker);
    }

    [Fact]
    public void AddMetricFlowTracker_StandaloneExtension_RegistersKeyedAndFacade()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Explicit separation of concepts: AddMetricFlow (facade) + AddMetricFlowTracker (tracker)
        services.AddMetricFlow();
        services.AddMetricFlowTracker("MyPlixConsole", opt =>
        {
            opt.TopicTags = new Dictionary<string, string> { ["tier"] = "worker" };
        });

        using var provider = services.BuildServiceProvider();

        var metricFlow = provider.GetRequiredService<IMetricFlow>();
        var tracker = metricFlow.GetTracker("MyPlixConsole");
        tracker.Should().NotBeNull();
        tracker.TopicTags.Should().ContainKey("tier").WhoseValue.Should().Be("worker");

        // Keyed resolution
        var keyed = provider.GetRequiredKeyedService<IMetricTracker>("MyPlixConsole");
        keyed.Should().BeSameAs(tracker);
    }

    [Fact]
    public void MetricFlow_GetTracker_DynamicTopic_CreatesAndCachesTracker()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetricFlow();

        using var provider = services.BuildServiceProvider();
        var metricFlow = provider.GetRequiredService<IMetricFlow>();

        // Act - Retrieve tracker that wasn't statically configured in DI
        var dynamicTracker = metricFlow.GetTracker("DynamicPipeline_42");

        // Assert
        dynamicTracker.Should().NotBeNull();
        dynamicTracker.Topic.Should().Be("DynamicPipeline_42");

        // Cache verification - subsequent call returns the exact same instance
        var cachedTracker = metricFlow.GetTracker("DynamicPipeline_42");
        cachedTracker.Should().BeSameAs(dynamicTracker);

        metricFlow.TryGetTracker("DynamicPipeline_42", out var found).Should().BeTrue();
        found.Should().BeSameAs(dynamicTracker);
    }

    [Fact]
    public void FluentBuilder_Current_ReturnsUnderlyingServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddMetricFlow();

        // Assert - IFluentBuilder<IServiceCollection>
        builder.Current.Should().BeSameAs(services);
        builder.Services.Should().BeSameAs(services);
    }

    [Fact]
    public void UserScenario_ExampleCode_WorksSeamlessly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetricFlow()
            .AddMetricTracker("MyPlixConsole");

        using var provider = services.BuildServiceProvider();
        var _metricFlow = provider.GetRequiredService<IMetricFlow>();

        // Act - User's exact example:
        var tracker = _metricFlow.GetTracker("MyPlixConsole");
        using var operation = tracker.Track("IngestEpg");

        // Assert
        tracker.Topic.Should().Be("MyPlixConsole");
        operation.Should().NotBeNull();
    }
}
