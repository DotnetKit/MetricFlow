using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MetricFlow.Tests;

public class MetricFlowOptionsTests
{
    [Fact]
    public void AddTagEnricher_SingleEnricher_InitializesAndSetsTopicTags()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options.AddTagEnricher(tags =>
        {
            tags["env"] = "production";
            tags["region"] = "us-east-1";
        });

        // Assert
        options.TopicTags.Should().NotBeNull();
        options.TopicTags.Should().ContainKey("env").WhoseValue.Should().Be("production");
        options.TopicTags.Should().ContainKey("region").WhoseValue.Should().Be("us-east-1");
    }

    [Fact]
    public void AddTagEnricher_MultipleEnrichers_ChainsAndMergesTags()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options
            .AddTagEnricher(tags => tags["service"] = "order-api")
            .AddTagEnricher(tags => tags["cluster"] = "k8s-prod")
            .AddTagEnricher(tags => tags["service"] = "order-service"); // overwrite

        // Assert
        options.TopicTags.Should().NotBeNull();
        options.TopicTags.Should().HaveCount(2);
        options.TopicTags.Should().ContainKey("service").WhoseValue.Should().Be("order-service");
        options.TopicTags.Should().ContainKey("cluster").WhoseValue.Should().Be("k8s-prod");
    }

    [Fact]
    public void AddTagEnricher_NullDelegate_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act & Assert
        FluentActions.Invoking(() => options.AddTagEnricher(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDimensionCounter_SingleTag_AddsDimensionCounterToConfiguredCounters()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options.AddDimensionCounter("country");

        // Assert
        options.Counters.Should().ContainSingle(c => c is DimensionCounter);
        var counter = options.Counters.OfType<DimensionCounter>().Single();
        counter.Name.Should().Be("Dimension:country");
        counter.DimensionName.Should().Be("country");
        counter.MaxUniqueValues.Should().Be(250);
        counter.OverflowBucket.Should().Be("[Other]");
    }

    [Fact]
    public void AddDimensionCounter_SingleTag_WithCustomOptions_ConfiguresProperly()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options.AddDimensionCounter(
            dimensionKey: "status_code",
            name: "HttpStatus",
            maxUniqueValues: 50,
            overflowBucket: "[Overflow]");

        // Assert
        var counter = options.Counters.OfType<DimensionCounter>().Single();
        counter.Name.Should().Be("HttpStatus");
        counter.DimensionName.Should().Be("status_code");
        counter.MaxUniqueValues.Should().Be(50);
        counter.OverflowBucket.Should().Be("[Overflow]");
    }

    [Fact]
    public void AddDimensionCounter_CompositeMultiTags_AddsMultiTagDimensionCounter()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options.AddDimensionCounter(
            name: "CountryAndPayment",
            dimensionKeys: ["country", "payment_method"],
            delimiter: " - ",
            maxUniqueValues: 100,
            overflowBucket: "[OtherCombinations]");

        // Assert
        var counter = options.Counters.OfType<DimensionCounter>().Single();
        counter.Name.Should().Be("CountryAndPayment");
        counter.MaxUniqueValues.Should().Be(100);
        counter.OverflowBucket.Should().Be("[OtherCombinations]");
    }

    [Fact]
    public void AddDimensionCounter_ComputedSelector_AddsLambdaDimensionCounter()
    {
        // Arrange
        var options = new MetricFlowOptions();

        // Act
        options.AddDimensionCounter(
            name: "OrderTier",
            selector: (_, meta) =>
            {
                var amount = meta?.GetValueOrDefault("amount") ?? 0;
                return amount > 1000 ? "High" : "Standard";
            },
            maxUniqueValues: 10,
            overflowBucket: "[TierOverflow]",
            dimensionName: "CustomTier");

        // Assert
        var counter = options.Counters.OfType<DimensionCounter>().Single();
        counter.Name.Should().Be("OrderTier");
        counter.DimensionName.Should().Be("CustomTier");
        counter.MaxUniqueValues.Should().Be(10);
        counter.OverflowBucket.Should().Be("[TierOverflow]");
    }

    [Fact]
    public void AddTagEnricher_And_AddDimensionCounter_ConfiguredViaServiceCollection_WorkEndToEnd()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetricFlow("BillingService", options =>
        {
            options.AddTagEnricher(tags =>
            {
                tags["environment"] = "staging";
                tags["region"] = "eu-west-1";
            })
            .AddDimensionCounter("tenant")
            .AddDimensionCounter("CountryPayment", ["country", "payment_method"]);
        });

        using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<IMetricTracker>();

        // Act - TopicTags verification
        tracker.TopicTags.Should().NotBeNull();
        tracker.TopicTags.Should().ContainKey("environment").WhoseValue.Should().Be("staging");
        tracker.TopicTags.Should().ContainKey("region").WhoseValue.Should().Be("eu-west-1");

        // Act - Track operations with dimension tags
        using (tracker.Track("InvoiceCreated", new() { ["tenant"] = "AcmeCorp", ["country"] = "DE", ["payment_method"] = "SEPA" }))
        {
        }

        // Assert - Dimension snapshots
        var tenantSnapshot = tracker.GetDimensionValues("InvoiceCreated", "tenant");
        tenantSnapshot.Should().NotBeNull();
        tenantSnapshot.Breakdown.Should().ContainKey("AcmeCorp").WhoseValue.Should().Be(1);

        var compositeSnapshot = tracker.GetDimensionValues("InvoiceCreated", "CountryPayment");
        compositeSnapshot.Should().NotBeNull();
        compositeSnapshot.Breakdown.Should().ContainKey("DE / SEPA").WhoseValue.Should().Be(1);
    }
}
