using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Counters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class DimensionCounterTests
{
    [Fact]
    public void DimensionCounter_PrimaryApi_ShouldTrackAndProduceDimensionSnapshot()
    {
        // Arrange
        var tracker = new MetricTracker("DimensionTopic")
            .AddDimensionCounter("tenant");

        // Act
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantA" })) { }
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantB" })) { }
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantA" })) { }

        // Assert
        var snapshot = tracker.GetDimensionValues("ProcessJob", "tenant");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(3);
        snapshot.TrackedOperations.Should().Be(3);
        snapshot.TaggedOperations.Should().Be(3);
        snapshot.UntrackedOperations.Should().Be(0);
        snapshot.Breakdown["TenantA"].Should().Be(2);
        snapshot.Breakdown["TenantB"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ShouldTrackTaggedAndUntaggedOperations()
    {
        // Arrange
        var tracker = new MetricTracker("TagTrackingTopic")
            .AddTagBreakdownCounter("country");

        // Act
        using (tracker.Track("ProcessOrder", new() { ["country"] = "US" })) { }
        using (tracker.Track("ProcessOrder", new() { ["country"] = "US" })) { }
        using (tracker.Track("ProcessOrder", new() { ["country"] = "DE" })) { }
        using (tracker.Track("ProcessOrder")) { } // untagged

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("ProcessOrder", "country");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(4);
        snapshot.TaggedOperations.Should().Be(3);
        snapshot.UntaggedOperations.Should().Be(1);
        snapshot.FailedOperations.Should().Be(0);
        snapshot.TaggedPercentage.Should().BeApproximately(0.75, 0.01);

        snapshot.Breakdown.Should().ContainKey("US");
        snapshot.Breakdown["US"].Should().Be(2);
        snapshot.Breakdown.Should().ContainKey("DE");
        snapshot.Breakdown["DE"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ScopeSetTag_ShouldTrackDynamicTags()
    {
        // Arrange
        var tracker = new MetricTracker("DynamicTagTopic")
            .AddTagBreakdownCounter("country");

        // Act
        using (var scope = tracker.Track("CreateShipment"))
        {
            scope.SetTag("country", "JP");
        }

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("CreateShipment", "country");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(1);
        snapshot.TaggedOperations.Should().Be(1);
        snapshot.Breakdown.Should().ContainKey("JP");
        snapshot.Breakdown["JP"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_CaseInsensitiveTagMatching_ShouldWork()
    {
        // Arrange
        var tracker = new MetricTracker("CaseInsensitiveTopic")
            .AddTagBreakdownCounter("country");

        // Act - provide key with different casing
        using (tracker.Track("OrderOp", new() { ["Country"] = "FR" })) { }
        using (tracker.Track("OrderOp", new() { ["COUNTRY"] = "fr" })) { }

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("OrderOp", "country");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(2);
        snapshot.TaggedOperations.Should().Be(2);
        snapshot.Breakdown.Should().ContainKey("FR");
        snapshot.Breakdown["FR"].Should().Be(2);
    }

    [Fact]
    public void TagBreakdownCounter_MetadataFallback_ShouldTrackNumericDimensions()
    {
        // Arrange
        var tracker = new MetricTracker("MetadataFallbackTopic")
            .AddTagBreakdownCounter("status_code");

        // Act - set numeric metadata rather than string tag
        using (var scope = tracker.Track("HandleRequest"))
        {
            scope.SetMetadata("status_code", 200);
        }

        using (var scope = tracker.Track("HandleRequest"))
        {
            scope.SetMetadata("status_code", 404);
        }

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("HandleRequest", "status_code");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(2);
        snapshot.TaggedOperations.Should().Be(2);
        snapshot.Breakdown.Should().ContainKey("200");
        snapshot.Breakdown["200"].Should().Be(1);
        snapshot.Breakdown.Should().ContainKey("404");
        snapshot.Breakdown["404"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_CardinalityLimit_ShouldRollIntoOverflowBucket()
    {
        // Arrange - max 3 unique values, bucket "[Other]"
        var tracker = new MetricTracker("CardinalityTopic")
            .AddTagBreakdownCounter("tenant_id", maxUniqueValues: 3, overflowBucket: "[Other]");

        // Act - insert 5 distinct values and repeat one
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_A" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_B" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_C" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_D" })) { } // overflow
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_E" })) { } // overflow
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_A" })) { } // existing key

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("TenantJob", "tenant_id");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(6);
        snapshot.TaggedOperations.Should().Be(6);

        // Should have 3 allowed keys + 1 overflow key
        snapshot.Breakdown.Count.Should().Be(4);
        snapshot.Breakdown["Tenant_A"].Should().Be(2);
        snapshot.Breakdown["Tenant_B"].Should().Be(1);
        snapshot.Breakdown["Tenant_C"].Should().Be(1);
        snapshot.Breakdown["[Other]"].Should().Be(2);
    }

    [Fact]
    public async Task TagBreakdownCounter_MultiThreadedConcurrency_ShouldBeThreadSafe()
    {
        // Arrange
        var tracker = new MetricTracker("ConcurrentTopic")
            .AddTagBreakdownCounter("country");

        var countries = new[] { "US", "DE", "FR", "JP", "UK" };
        const int concurrency = 20;
        const int iterationsPerTask = 50;

        // Act
        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            await Task.Yield();
            for (int j = 0; j < iterationsPerTask; j++)
            {
                var country = countries[(i + j) % countries.Length];
                using (tracker.Track("ConcurrOp", new() { ["country"] = country }))
                {
                    // simulate minimal work
                }
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("ConcurrOp", "country");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(concurrency * iterationsPerTask);
        snapshot.TaggedOperations.Should().Be(concurrency * iterationsPerTask);
        snapshot.UntaggedOperations.Should().Be(0);

        var totalBreakdownSum = snapshot.Breakdown.Values.Sum();
        totalBreakdownSum.Should().Be(concurrency * iterationsPerTask);

        foreach (var c in countries)
        {
            snapshot.Breakdown.Should().ContainKey(c);
            snapshot.Breakdown[c].Should().Be((concurrency * iterationsPerTask) / countries.Length);
        }
    }

    [Fact]
    public void TagBreakdownCounter_MultipleCountersOnSameTracker_ShouldTrackIndependentDimensions()
    {
        // Arrange - track both country and order_type
        var tracker = new MetricTracker("MultiDimensionTopic")
            .AddTagBreakdownCounter("country")
            .AddTagBreakdownCounter("order_type");

        // Act
        using (tracker.Track("Checkout", new() { ["country"] = "US", ["order_type"] = "retail" })) { }
        using (tracker.Track("Checkout", new() { ["country"] = "DE", ["order_type"] = "retail" })) { }
        using (tracker.Track("Checkout", new() { ["country"] = "US", ["order_type"] = "wholesale" })) { }

        // Assert - country
        var countrySnap = tracker.GetTagBreakdownValues("Checkout", "country");
        countrySnap.Should().NotBeNull();
        countrySnap!.Breakdown["US"].Should().Be(2);
        countrySnap.Breakdown["DE"].Should().Be(1);

        // Assert - order_type
        var typeSnap = tracker.GetTagBreakdownValues("Checkout", "order_type");
        typeSnap.Should().NotBeNull();
        typeSnap!.Breakdown["retail"].Should().Be(2);
        typeSnap.Breakdown["wholesale"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_SnapshotFormatting_ShouldContainExpectedSections()
    {
        // Arrange
        var tracker = new MetricTracker("FormatTopic")
            .AddTagBreakdownCounter("country");

        using (tracker.Track("Shipment", new() { ["country"] = "US" })) { }
        using (tracker.Track("Shipment", new() { ["country"] = "US" })) { }
        using (tracker.Track("Shipment", new() { ["country"] = "DE" })) { }
        using (tracker.Track("Shipment")) { } // untagged

        // Act
        var snapshot = tracker.GetTagBreakdownValues("Shipment", "country");
        var formatted = snapshot!.ToFormattedString();

        // Assert
        formatted.Should().Contain("[TagBreakdown:country] Metric: Shipment");
        formatted.Should().Contain("Total Operations       : 4");
        formatted.Should().Contain("Tagged Operations      : 3 (75.0%)");
        formatted.Should().Contain("Untagged Operations    : 1");
        formatted.Should().Contain("Breakdown by 'country':");
        formatted.Should().Contain("US: 2 (66.7%)");
        formatted.Should().Contain("DE: 1 (33.3%)");
    }

    [Fact]
    public void TagBreakdownCounter_Reset_ShouldClearStates()
    {
        // Arrange
        var tracker = new MetricTracker("ResetTopic")
            .AddTagBreakdownCounter("country");

        using (tracker.Track("ResetOp", new() { ["country"] = "US" })) { }

        // Act
        tracker.Clear();

        // Assert
        var snapshot = tracker.GetTagBreakdownValues("ResetOp", "country");
        snapshot.Should().BeNull();
    }

    [Fact]
    public void TagBreakdownCounter_Validation_ShouldThrowOnInvalidArgs()
    {
        // Act & Assert
        FluentActions.Invoking(() => new TagBreakdownCounter(""))
            .Should().Throw<ArgumentException>();

        FluentActions.Invoking(() => new TagBreakdownCounter("country", maxUniqueValues: 0))
            .Should().Throw<ArgumentOutOfRangeException>();

        FluentActions.Invoking(() => new TagBreakdownCounter("country", overflowBucket: ""))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TagBreakdownCounter_CompositeMultiTags_ShouldJoinValuesWithDelimiter()
    {
        // Arrange
        var tracker = new MetricTracker("MultiTagTopic")
            .AddTagBreakdownCounter("CountryAndPayment", ["country", "payment_method"]);

        // Act
        // 1. Both tags present
        using (tracker.Track("Checkout", new() { ["country"] = "US", ["payment_method"] = "CreditCard" })) { }
        using (tracker.Track("Checkout", new() { ["country"] = "US", ["payment_method"] = "CreditCard" })) { }

        // 2. Different combination
        using (tracker.Track("Checkout", new() { ["country"] = "DE", ["payment_method"] = "PayPal" })) { }

        // 3. Partial tag present (one missing)
        using (tracker.Track("Checkout", new() { ["country"] = "FR" })) { }

        // 4. Neither tag present
        using (tracker.Track("Checkout")) { }

        // Assert
        var snapshot = tracker.GetComputedBreakdownValues("Checkout", "CountryAndPayment");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(5);
        snapshot.TaggedOperations.Should().Be(4);
        snapshot.UntaggedOperations.Should().Be(1);

        snapshot.Breakdown.Should().ContainKey("US / CreditCard");
        snapshot.Breakdown["US / CreditCard"].Should().Be(2);

        snapshot.Breakdown.Should().ContainKey("DE / PayPal");
        snapshot.Breakdown["DE / PayPal"].Should().Be(1);

        snapshot.Breakdown.Should().ContainKey("FR / -");
        snapshot.Breakdown["FR / -"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ComputedSelector_ShouldComputeCustomCategories()
    {
        // Arrange - compute tier based on metadata and tags
        var tracker = new MetricTracker("ComputedTopic")
            .AddComputedBreakdownCounter("CustomerTier", (tags, metadata) =>
            {
                var country = tags?.GetValueOrDefault("country") ?? "Unknown";
                var amount = metadata?.GetValueOrDefault("amount") ?? 0;

                if (amount >= 1000) return $"VIP_{country}";
                if (amount >= 100) return $"Standard_{country}";
                return $"Economy_{country}";
            });

        // Act
        using (var scope = tracker.Track("ProcessOrder", new() { ["country"] = "US" }))
        {
            scope.SetMetadata("amount", 2000);
        }

        using (var scope = tracker.Track("ProcessOrder", new() { ["country"] = "US" }))
        {
            scope.SetMetadata("amount", 500);
        }

        using (var scope = tracker.Track("ProcessOrder", new() { ["country"] = "DE" }))
        {
            scope.SetMetadata("amount", 20);
        }

        // Assert
        var snapshot = tracker.GetComputedBreakdownValues("ProcessOrder", "CustomerTier");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(3);
        snapshot.TaggedOperations.Should().Be(3);
        snapshot.UntaggedOperations.Should().Be(0);

        snapshot.Breakdown.Should().ContainKey("VIP_US");
        snapshot.Breakdown["VIP_US"].Should().Be(1);

        snapshot.Breakdown.Should().ContainKey("Standard_US");
        snapshot.Breakdown["Standard_US"].Should().Be(1);

        snapshot.Breakdown.Should().ContainKey("Economy_DE");
        snapshot.Breakdown["Economy_DE"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ComputedSelector_ConditionalFilter_ShouldTrackMatchingOnly()
    {
        // Arrange - only count operations satisfying a business predicate (return null otherwise)
        var tracker = new MetricTracker("ConditionalTopic")
            .AddComputedBreakdownCounter("HighValueOrders", (tags, metadata) =>
            {
                var isHighValue = metadata?.GetValueOrDefault("amount") > 500;
                return isHighValue ? "HighValue" : null;
            });

        // Act
        // Matched operation
        using (var scope = tracker.Track("PlaceOrder"))
        {
            scope.SetMetadata("amount", 1000);
        }

        // Unmatched operation
        using (var scope = tracker.Track("PlaceOrder"))
        {
            scope.SetMetadata("amount", 100);
        }

        // Unmatched operation
        using (var scope = tracker.Track("PlaceOrder"))
        {
            scope.SetMetadata("amount", 250);
        }

        // Assert
        var snapshot = tracker.GetComputedBreakdownValues("PlaceOrder", "HighValueOrders");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(3);
        snapshot.TaggedOperations.Should().Be(1);
        snapshot.UntaggedOperations.Should().Be(2);

        snapshot.Breakdown.Should().ContainKey("HighValue");
        snapshot.Breakdown["HighValue"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ComputedSelector_WhenLambdaThrows_ShouldNotCrashScope()
    {
        // Arrange - lambda that deliberately throws an exception
        var tracker = new MetricTracker("FaultySelectorTopic")
            .AddComputedBreakdownCounter("FaultySelector", (tags, metadata) =>
            {
                throw new InvalidOperationException("Simulation error in user lambda");
            });

        // Act - should execute and dispose smoothly without throwing
        FluentActions.Invoking(() =>
        {
            using (tracker.Track("FaultyOp"))
            {
                // Work
            }
        }).Should().NotThrow();

        // Assert - operation recorded safely as untagged
        var snapshot = tracker.GetComputedBreakdownValues("FaultyOp", "FaultySelector");
        snapshot.Should().NotBeNull();
        snapshot!.TotalOperations.Should().Be(1);
        snapshot.TaggedOperations.Should().Be(0);
        snapshot.UntaggedOperations.Should().Be(1);
    }
}
