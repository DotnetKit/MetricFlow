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
        var tracker = new MetricTracker("DimensionTopic").AddDimensionCounter("tenant");

        // Act
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantA" })) { }
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantB" })) { }
        using (tracker.Track("ProcessJob", new() { ["tenant"] = "TenantA" })) { }

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("ProcessJob", "tenant");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(3);
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
        var tracker = new MetricTracker("TagTrackingTopic").AddDimensionCounter("country");

        // Act
        using (tracker.Track("ProcessOrder", new() { ["country"] = "US" })) { }
        using (tracker.Track("ProcessOrder", new() { ["country"] = "US" })) { }
        using (tracker.Track("ProcessOrder", new() { ["country"] = "DE" })) { }
        using (tracker.Track("ProcessOrder")) { } // untagged

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("ProcessOrder", "country");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(4);
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
        var tracker = new MetricTracker("DynamicTagTopic").AddDimensionCounter("country");

        // Act
        using (var scope = tracker.Track("CreateShipment"))
        {
            scope.SetTag("country", "JP");
        }

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("CreateShipment", "country");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TaggedOperations.Should().Be(1);
        snapshot.Breakdown.Should().ContainKey("JP");
        snapshot.Breakdown["JP"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_CaseInsensitiveTagMatching_ShouldWork()
    {
        // Arrange
        var tracker = new MetricTracker("CaseInsensitiveTopic").AddDimensionCounter("country");

        // Act - provide key with different casing
        using (tracker.Track("OrderOp", new() { ["Country"] = "FR" })) { }
        using (tracker.Track("OrderOp", new() { ["COUNTRY"] = "fr" })) { }

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("OrderOp", "country");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(2);
        snapshot.TaggedOperations.Should().Be(2);
        snapshot.Breakdown.Should().ContainKey("FR");
        snapshot.Breakdown["FR"].Should().Be(2);
    }

    [Fact]
    public void TagBreakdownCounter_MetadataFallback_ShouldTrackNumericDimensions()
    {
        // Arrange
        var tracker = new MetricTracker("MetadataFallbackTopic").AddDimensionCounter("status_code");

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
        var snapshot = tracker.GetDimensionSnapshot("HandleRequest", "status_code");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(2);
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
        var tracker = new MetricTracker("CardinalityTopic").AddDimensionCounter(
            "tenant_id",
            maxUniqueValues: 3,
            overflowBucket: "[Other]"
        );

        // Act - insert 5 distinct values and repeat one
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_A" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_B" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_C" })) { }
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_D" })) { } // overflow
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_E" })) { } // overflow
        using (tracker.Track("TenantJob", new() { ["tenant_id"] = "Tenant_A" })) { } // existing key

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("TenantJob", "tenant_id");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(6);
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
        var tracker = new MetricTracker("ConcurrentTopic").AddDimensionCounter("country");

        var countries = new[] { "US", "DE", "FR", "JP", "UK" };
        const int concurrency = 20;
        const int iterationsPerTask = 50;

        // Act
        var tasks = Enumerable
            .Range(0, concurrency)
            .Select(async i =>
            {
                await Task.Yield();
                for (var j = 0; j < iterationsPerTask; j++)
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
        var snapshot = tracker.GetDimensionSnapshot("ConcurrOp", "country");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(concurrency * iterationsPerTask);
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
            .AddDimensionCounter("country")
            .AddDimensionCounter("order_type");

        // Act
        using (tracker.Track("Checkout", new() { ["country"] = "US", ["order_type"] = "retail" }))
        { }
        using (tracker.Track("Checkout", new() { ["country"] = "DE", ["order_type"] = "retail" }))
        { }
        using (
            tracker.Track("Checkout", new() { ["country"] = "US", ["order_type"] = "wholesale" })
        ) { }

        // Assert - country
        var countrySnap = tracker.GetDimensionSnapshot("Checkout", "country");
        countrySnap.Should().NotBeNull();
        countrySnap.Breakdown["US"].Should().Be(2);
        countrySnap.Breakdown["DE"].Should().Be(1);

        // Assert - order_type
        var typeSnap = tracker.GetDimensionSnapshot("Checkout", "order_type");
        typeSnap.Should().NotBeNull();
        typeSnap.Breakdown["retail"].Should().Be(2);
        typeSnap.Breakdown["wholesale"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_SnapshotFormatting_ShouldContainExpectedSections()
    {
        // Arrange
        var tracker = new MetricTracker("FormatTopic").AddDimensionCounter("country");

        using (tracker.Track("Shipment", new() { ["country"] = "US" })) { }
        using (tracker.Track("Shipment", new() { ["country"] = "US" })) { }
        using (tracker.Track("Shipment", new() { ["country"] = "DE" })) { }
        using (tracker.Track("Shipment")) { } // untagged

        // Act
        var snapshot = tracker.GetDimensionSnapshot("Shipment", "country");
        var formatted = snapshot?.ToFormattedString();

        // Assert
        formatted.Should().Contain("[Dimension:country] Metric: Shipment");
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
        var tracker = new MetricTracker("ResetTopic").AddDimensionCounter("country");

        using (tracker.Track("ResetOp", new() { ["country"] = "US" })) { }

        // Act
        tracker.Clear();

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("ResetOp", "country");
        snapshot.Should().BeNull();
    }

    [Fact]
    public void TagBreakdownCounter_Validation_ShouldThrowOnInvalidArgs()
    {
        // Act & Assert
        FluentActions
            .Invoking(() => new TagBreakdownCounter(""))
            .Should()
            .Throw<ArgumentException>();

        FluentActions
            .Invoking(() => new TagBreakdownCounter("country", maxUniqueValues: 0))
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        FluentActions
            .Invoking(() => new TagBreakdownCounter("country", overflowBucket: ""))
            .Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void TagBreakdownCounter_CompositeMultiTags_ShouldJoinValuesWithDelimiter()
    {
        // Arrange
        var tracker = new MetricTracker("MultiTagTopic").AddDimensionCounter(
            "CountryAndPayment",
            ["country", "payment_method"]
        );

        // Act
        // 1. Both tags present
        using (
            tracker.Track(
                "Checkout",
                new() { ["country"] = "US", ["payment_method"] = "CreditCard" }
            )
        ) { }
        using (
            tracker.Track(
                "Checkout",
                new() { ["country"] = "US", ["payment_method"] = "CreditCard" }
            )
        ) { }

        // 2. Different combination
        using (
            tracker.Track("Checkout", new() { ["country"] = "DE", ["payment_method"] = "PayPal" })
        ) { }

        // 3. Partial tag present (one missing)
        using (tracker.Track("Checkout", new() { ["country"] = "FR" })) { }

        // 4. Neither tag present
        using (tracker.Track("Checkout")) { }

        // Assert
        var snapshot = tracker.GetDimensionSnapshot("Checkout", "CountryAndPayment");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(5);
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
        var tracker = new MetricTracker("ComputedTopic").AddDimensionCounter(
            "CustomerTier",
            (tags, metadata) =>
            {
                var country = tags?.GetValueOrDefault("country") ?? "Unknown";
                var amount = metadata?.GetValueOrDefault("amount") ?? 0;

                if (amount >= 1000)
                    return $"VIP_{country}";
                if (amount >= 100)
                    return $"Standard_{country}";
                return $"Economy_{country}";
            }
        );

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
        var snapshot = tracker.GetDimensionSnapshot("ProcessOrder", "CustomerTier");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(3);
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
        var tracker = new MetricTracker("ConditionalTopic").AddDimensionCounter(
            "HighValueOrders",
            (_, metadata) =>
            {
                var isHighValue = metadata?.GetValueOrDefault("amount") > 500;
                return isHighValue ? "HighValue" : null;
            }
        );

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
        var snapshot = tracker.GetDimensionSnapshot("PlaceOrder", "HighValueOrders");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(3);
        snapshot.TaggedOperations.Should().Be(1);
        snapshot.UntaggedOperations.Should().Be(2);

        snapshot.Breakdown.Should().ContainKey("HighValue");
        snapshot.Breakdown["HighValue"].Should().Be(1);
    }

    [Fact]
    public void TagBreakdownCounter_ComputedSelector_WhenLambdaThrows_ShouldNotCrashScope()
    {
        // Arrange - lambda that deliberately throws an exception
        var tracker = new MetricTracker("FaultySelectorTopic").AddDimensionCounter(
            "FaultySelector",
            (_, _) => throw new InvalidOperationException("Simulation error in user lambda")
        );

        // Act - should execute and dispose smoothly without throwing
        FluentActions
            .Invoking(() =>
            {
                using (tracker.Track("FaultyOp"))
                {
                    // Work
                }
            })
            .Should()
            .NotThrow();

        // Assert - operation recorded safely as untagged
        var snapshot = tracker.GetDimensionSnapshot("FaultyOp", "FaultySelector");
        snapshot.Should().NotBeNull();
        snapshot.TotalOperations.Should().Be(1);
        snapshot.TaggedOperations.Should().Be(0);
        snapshot.UntaggedOperations.Should().Be(1);
    }

    [Fact]
    public void DimensionCounter_PublicPropertiesAndStateAccess_ShouldBeCovered()
    {
        // Arrange
        var counter = new DimensionCounter(
            "region",
            "CustomDimCounter",
            maxUniqueValues: 50,
            overflowBucket: "[Extra]"
        );

        // Verify DimensionCounter public properties
        counter.DimensionName.Should().Be("region");
        counter.MaxUniqueValues.Should().Be(50);
        counter.OverflowBucket.Should().Be("[Extra]");
        counter.Name.Should().Be("CustomDimCounter");

        // Non-existent state should return null
        counter.GetState("NonExistent").Should().BeNull();

        // Record some operations directly via OnIn and OnOut
        var inCtx = new DotnetKit.MetricFlow.Abstractions.InContext("ProcessPayment");
        var inState = counter.OnIn(in inCtx);
        inState.Should().BeNull();

        var outCtx1 = new DotnetKit.MetricFlow.Abstractions.OutContext(
            "ProcessPayment",
            failed: false,
            exception: null,
            duration: TimeSpan.FromMilliseconds(10),
            tags: new Dictionary<string, string> { ["region"] = "EU" }
        );
        counter.OnOut(inState, in outCtx1);

        var outCtx2 = new DotnetKit.MetricFlow.Abstractions.OutContext(
            "ProcessPayment",
            failed: true,
            exception: null,
            duration: TimeSpan.FromMilliseconds(15),
            tags: new Dictionary<string, string> { ["region"] = "US" }
        );
        counter.OnOut(inState, in outCtx2);

        var outCtxUntagged = new DotnetKit.MetricFlow.Abstractions.OutContext(
            "ProcessPayment",
            failed: false,
            exception: null,
            duration: TimeSpan.FromMilliseconds(5),
            tags: null
        );
        counter.OnOut(inState, in outCtxUntagged);

        // Act - retrieve MetricDimensionState
        var state = counter.GetState("ProcessPayment");

        // Assert - state public properties
        state.Should().NotBeNull();
        state.MetricName.Should().Be("ProcessPayment");
        state.DimensionName.Should().Be("region");
        state.MaxUniqueValues.Should().Be(50);
        state.OverflowBucket.Should().Be("[Extra]");
        state.TotalOperations.Should().Be(3);
        state.TaggedOperations.Should().Be(2);
        state.UntaggedOperations.Should().Be(1);
        state.FailedOperations.Should().Be(1);

        var breakdown = state.GetBreakdown();
        breakdown.Should().ContainKey("EU").WhoseValue.Should().Be(1);
        breakdown.Should().ContainKey("US").WhoseValue.Should().Be(1);

        // Verify GetAllSnapshots
        var allSnapshots = counter.GetAllSnapshots().ToList();
        allSnapshots.Should().ContainSingle();
        allSnapshots[0].MetricName.Should().Be("ProcessPayment");

        // When IsEnabled is false, OnOut should be ignored
        counter.IsEnabled = false;
        counter.OnOut(inState, in outCtx1);
        state.TotalOperations.Should().Be(3);

        // Empty keys in composite selector should return null
        var emptyCompositeCounter = new DimensionCounter("EmptyKeys", Array.Empty<string>());
        emptyCompositeCounter.OnOut(inState, in outCtx1);
        var emptySnap = emptyCompositeCounter.GetSnapshot("ProcessPayment");
        emptySnap.Should().NotBeNull();
        ((DimensionSnapshot)emptySnap).UntaggedOperations.Should().Be(1);
    }

    [Fact]
    public void DimensionSnapshot_PublicPropertiesAndFormatting_ShouldBeCovered()
    {
        // Snapshot with failed operations and breakdown
        var now = DateTime.UtcNow;
        var breakdown = new Dictionary<string, long> { ["NA"] = 5, ["SA"] = 2 };
        var snapshot = new DimensionSnapshot(
            MetricName: "ProcessOrder",
            CounterName: "Dimension:region",
            DimensionName: "region",
            TotalOperations: 10,
            TaggedOperations: 7,
            UntaggedOperations: 3,
            FailedOperations: 2,
            Breakdown: breakdown,
            Timestamp: now
        );

        // Public properties
        snapshot.TagKey.Should().Be("region");
        snapshot.DimensionName.Should().Be("region");
        snapshot.TrackedOperations.Should().Be(7);
        snapshot.UntrackedOperations.Should().Be(3);
        snapshot.TrackedPercentage.Should().BeApproximately(0.7, 0.01);
        snapshot.TaggedPercentage.Should().BeApproximately(0.7, 0.01);
        snapshot.Timestamp.Should().Be(now);

        // ToString() delegating to ToFormattedString()
        var text = snapshot.ToString();
        text.Should().Contain("Failed Operations      : 2");
        text.Should().Contain("Untagged Operations    : 3");
        text.Should().Contain("Breakdown by 'region':");
        text.Should().Contain("NA: 5 (71.4%)");
        text.Should().Contain("SA: 2 (28.6%)");

        // Zero total operations branch
        var zeroSnapshot = new DimensionSnapshot(
            "EmptyMetric",
            "EmptyCounter",
            "dim",
            0,
            0,
            0,
            0,
            new Dictionary<string, long>(),
            now
        );
        zeroSnapshot.TaggedPercentage.Should().Be(0.0);
        zeroSnapshot.TrackedPercentage.Should().Be(0.0);
        var zeroFormatted = zeroSnapshot.ToFormattedString();
        zeroFormatted.Should().NotContain("Breakdown by");
        zeroFormatted.Should().NotContain("Failed Operations");
        zeroFormatted.Should().NotContain("Untagged Operations");
    }

    [Fact]
    public void TagBreakdownSnapshot_And_TagBreakdownCounter_Constructors_ShouldBeCovered()
    {
        // TagBreakdownSnapshot record coverage
        var now = DateTime.UtcNow;
        var tagSnapshot = new TagBreakdownSnapshot(
            MetricName: "JobOp",
            CounterName: "TagBreakdown:env",
            DimensionName: "env",
            TotalOperations: 4,
            TaggedOperations: 4,
            UntaggedOperations: 0,
            FailedOperations: 0,
            Breakdown: new Dictionary<string, long> { ["Prod"] = 4 },
            Timestamp: now
        );

        tagSnapshot.MetricName.Should().Be("JobOp");
        tagSnapshot.TagKey.Should().Be("env");
        tagSnapshot.TrackedPercentage.Should().Be(1.0);
        tagSnapshot.Breakdown["Prod"].Should().Be(4);

        // TagBreakdownCounter composite constructor
        var compositeCounter = new TagBreakdownCounter(
            "Composite",
            ["tenant", "region"],
            maxUniqueValues: 100,
            overflowBucket: "[Overflow]"
        );
        compositeCounter.DimensionName.Should().Be("tenant / region");
        compositeCounter.MaxUniqueValues.Should().Be(100);
        compositeCounter.OverflowBucket.Should().Be("[Overflow]");

        // TagBreakdownCounter lambda selector constructor
        var computedCounter = new TagBreakdownCounter(
            "CustomSelectorCounter",
            (tags, meta) => tags?.GetValueOrDefault("tier"),
            maxUniqueValues: 150,
            overflowBucket: "[OtherTier]",
            dimensionName: "TierDimension"
        );
        computedCounter.DimensionName.Should().Be("TierDimension");
        computedCounter.MaxUniqueValues.Should().Be(150);
        computedCounter.OverflowBucket.Should().Be("[OtherTier]");
    }
}
