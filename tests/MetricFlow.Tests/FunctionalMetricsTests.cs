using System.Diagnostics.Metrics;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Exporters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MetricFlow.Tests;

public class FunctionalMetricsTests
{
    [Fact]
    public void MetricTracker_ByDefault_HasZeroTelemetryCounters()
    {
        // Arrange & Act
        using var tracker = new MetricTracker("Orders");

        // Assert
        tracker.GetCounters().Should().BeEmpty();
        tracker.DurationCounter.Should().BeNull();

        using (tracker.Track("PlaceOrder")) { }
        tracker.GetDurationSnapshot("PlaceOrder").Should().BeNull();
        tracker.GetExceptionSnapshot("PlaceOrder").Should().BeNull();
        tracker.GetAllSnapshots().Should().BeEmpty();
    }

    [Fact]
    public void AddFunctionalMetrics_RecordsAndIncrementsWithoutTelemetryOverhead()
    {
        // Arrange
        using var tracker = new MetricTracker("Orders")
            .AddFunctionalMetrics("country", "plan");

        // Act
        tracker.Record("OrderPlaced", new() { ["country"] = "FR", ["plan"] = "Pro" });
        tracker.Record("OrderPlaced", new() { ["country"] = "US", ["plan"] = "Free" });
        tracker.Increment("OrderPlaced", 3, new() { ["country"] = "FR", ["plan"] = "Pro" });

        // Assert - No telemetry counters registered
        tracker.DurationCounter.Should().BeNull();
        tracker.GetDurationSnapshot("OrderPlaced").Should().BeNull();
        tracker.GetMemorySnapshot("OrderPlaced").Should().BeNull();
        tracker.GetExceptionSnapshot("OrderPlaced").Should().BeNull();

        // Assert - CountCounter accumulated total count and event count
        var count = tracker.GetCountSnapshot("OrderPlaced");
        count.Should().NotBeNull();
        count!.TotalCount.Should().Be(5);
        count.EventCount.Should().Be(3);

        // Assert - DimensionCounters sliced by count
        var byCountry = tracker.GetDimensionSnapshot("OrderPlaced", "country");
        byCountry.Should().NotBeNull();
        byCountry!.TotalOperations.Should().Be(5);
        byCountry.Breakdown["FR"].Should().Be(4);
        byCountry.Breakdown["US"].Should().Be(1);

        var byPlan = tracker.GetDimensionSnapshot("OrderPlaced", "plan");
        byPlan.Should().NotBeNull();
        byPlan!.Breakdown["Pro"].Should().Be(4);
        byPlan.Breakdown["Free"].Should().Be(1);
    }

    [Fact]
    public void MixedTracker_RecordAndIncrement_IgnoreTelemetryCounters_WhileTrackFeedsBoth()
    {
        // Arrange
        using var tracker = new MetricTracker("Billing")
            .AddTelemetryCounters(includeMemory: true, includeHierarchy: true)
            .AddFunctionalMetrics("currency");

        // Act 1 - Point-in-time functional recording
        tracker.Record("InvoiceIssued", new() { ["currency"] = "EUR" });
        tracker.Increment("InvoiceIssued", 4, new() { ["currency"] = "USD" });

        // Assert 1 - Telemetry counters ignored point-in-time recording
        tracker.GetDurationSnapshot("InvoiceIssued").Should().BeNull();
        tracker.GetMemorySnapshot("InvoiceIssued").Should().BeNull();
        tracker.GetExceptionSnapshot("InvoiceIssued").Should().BeNull();
        tracker.GetHierarchySnapshot("InvoiceIssued").Should().BeNull();

        var invoiceCount = tracker.GetCountSnapshot("InvoiceIssued");
        invoiceCount.Should().NotBeNull();
        invoiceCount!.TotalCount.Should().Be(5);
        invoiceCount.EventCount.Should().Be(2);

        var currencyDim = tracker.GetDimensionSnapshot("InvoiceIssued", "currency");
        currencyDim.Should().NotBeNull();
        currencyDim!.Breakdown["EUR"].Should().Be(1);
        currencyDim.Breakdown["USD"].Should().Be(4);

        // Act 2 - Scoped operation tracking feeds both telemetry and functional counters
        using (tracker.Track("ProcessPayment", new() { ["currency"] = "EUR" }))
        {
            var buf = new byte[1024];
            _ = buf.Length;
        }

        // Assert 2
        tracker.GetDurationSnapshot("ProcessPayment").Should().NotBeNull();
        tracker.GetMemorySnapshot("ProcessPayment").Should().NotBeNull();
        tracker.GetExceptionSnapshot("ProcessPayment").Should().NotBeNull();
        tracker.GetCountSnapshot("ProcessPayment")!.TotalCount.Should().Be(1);
        tracker.GetDimensionSnapshot("ProcessPayment", "currency")!.Breakdown["EUR"].Should().Be(1);
    }

    [Fact]
    public void RecordAndIncrement_EmitCounterLongToMeterBridge()
    {
        // Arrange
        var recordedMeasurements = new List<(string InstrumentName, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.FunctionalBridgeTopic")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in tags)
            {
                tagDict[kv.Key] = kv.Value;
            }
            recordedMeasurements.Add((instrument.Name, measurement, tagDict));
        });
        listener.Start();

        using var tracker = new MetricTracker("FunctionalBridgeTopic")
            .AddFunctionalMetrics("region");

        // Act
        tracker.Record("OrderCreated", new() { ["region"] = "EU" });
        tracker.Increment("OrderCreated", 5, new() { ["region"] = "US" });

        // Assert
        recordedMeasurements.Should().HaveCount(2);
        recordedMeasurements[0].InstrumentName.Should().Be("OrderCreated.total");
        recordedMeasurements[0].Value.Should().Be(1);
        recordedMeasurements[0].Tags["region"].Should().Be("EU");

        recordedMeasurements[1].InstrumentName.Should().Be("OrderCreated.total");
        recordedMeasurements[1].Value.Should().Be(5);
        recordedMeasurements[1].Tags["region"].Should().Be("US");
    }

    [Fact]
    public void OperationSummaryAndMarkdown_WorkCleanlyWithPureFunctionalMetrics()
    {
        // Arrange
        using var tracker = new MetricTracker("Analytics")
            .AddFunctionalMetrics("tier");

        tracker.Increment("Signup", 10, new() { ["tier"] = "Pro" });
        tracker.Increment("Signup", 2, new() { ["tier"] = "Enterprise" });

        // Act
        var summary = tracker.GetOperationSummary("Signup");
        var markdown = tracker.ToMarkdownTable();

        // Assert
        summary.Should().NotBeNull();
        summary!.OperationName.Should().Be("Signup");
        summary.TotalOperations.Should().Be(12);
        summary.Count.Should().NotBeNull();
        summary.Count!.TotalCount.Should().Be(12);
        summary.Count.EventCount.Should().Be(2);
        summary.Duration.Should().BeNull();
        summary.Memory.Should().BeNull();
        summary.Dimensions.Should().ContainSingle(d => d.DimensionName == "tier");

        markdown.Should().Contain("Signup");
        markdown.Should().Contain("12");
        markdown.Should().Contain("Pro");
    }

    [Fact]
    public void DependencyInjection_AddTelemetryCountersAndAddFunctionalMetrics_WorkPerTopic()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .AddMetricFlow("HttpTelemetry", opt => opt.AddTelemetryCounters(includeMemory: true))
            .AddMetricTracker("BusinessEvents", opt => opt.AddFunctionalMetrics("status"));

        using var provider = services.BuildServiceProvider();
        var flow = provider.GetRequiredService<IMetricFlow>();

        // Act
        var telemetryTracker = flow.GetTracker("HttpTelemetry");
        using (telemetryTracker.Track("GET /orders")) { }

        var businessTracker = flow.GetTracker("BusinessEvents");
        businessTracker.Increment("OrdersPaid", 7, new() { ["status"] = "settled" });

        // Assert
        telemetryTracker.GetDurationSnapshot("GET /orders").Should().NotBeNull();
        telemetryTracker.GetMemorySnapshot("GET /orders").Should().NotBeNull();
        telemetryTracker.GetExceptionSnapshot("GET /orders").Should().NotBeNull();

        businessTracker.GetDurationSnapshot("OrdersPaid").Should().BeNull();
        businessTracker.GetCountSnapshot("OrdersPaid")!.TotalCount.Should().Be(7);
        businessTracker.GetDimensionSnapshot("OrdersPaid", "status")!.Breakdown["settled"].Should().Be(7);
    }
}
