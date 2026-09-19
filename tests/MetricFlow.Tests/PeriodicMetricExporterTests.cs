using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class PeriodicMetricExporterTests
{
    private class TestSink : IMetricSink
    {
        public string Name => "TestSink";
        public List<MetricTimelineEntry> EmittedEntries { get; } = new();

        public ValueTask EmitAsync(IReadOnlyList<MetricTimelineEntry> entries, CancellationToken cancellationToken = default)
        {
            EmittedEntries.AddRange(entries);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task HarvestAsync_ComputesDeltaAndDispatchesToSinks()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");
        var sink = new TestSink();
        var options = new PeriodicMetricExporterOptions
        {
            Resource = new ResourceMetadata("MyService", "pod-123", "pod-123", "Staging")
        };

        var exporter = new PeriodicMetricExporter(tracker, new[] { sink }, options);

        // Perform 5 operations
        for (int i = 0; i < 5; i++)
        {
            using (tracker.Track("Checkout"))
            {
                Thread.Sleep(2);
            }
        }

        // Act - First harvest
        var entries1 = await exporter.HarvestAsync();

        // Assert - First harvest has 5 operations
        entries1.Should().NotBeEmpty();
        sink.EmittedEntries.Should().HaveCount(entries1.Count);

        var checkoutEntry1 = entries1.First(e => e.MetricName == "Checkout" && e.CounterName == DurationCounter.DefaultCounterName);
        var delta1 = (DurationSnapshot)checkoutEntry1.Delta;
        delta1.OutCount.Should().Be(5);
        checkoutEntry1.Resource.Should().NotBeNull();
        checkoutEntry1.Resource!.InstanceId.Should().Be("pod-123");

        // Act - Perform 3 more operations
        for (int i = 0; i < 3; i++)
        {
            using (tracker.Track("Checkout"))
            {
                Thread.Sleep(2);
            }
        }

        // Act - Second harvest
        var entries2 = await exporter.HarvestAsync();

        // Assert - Second harvest delta should reflect strictly the 3 new operations
        var checkoutEntry2 = entries2.First(e => e.MetricName == "Checkout" && e.CounterName == DurationCounter.DefaultCounterName);
        var delta2 = (DurationSnapshot)checkoutEntry2.Delta;
        var cumulative2 = (DurationSnapshot)checkoutEntry2.Cumulative;

        delta2.OutCount.Should().Be(3);
        cumulative2.OutCount.Should().Be(8);
    }

    [Fact]
    public async Task HarvestAsync_FiltersZeroActivityMetrics_WhenOptionIsDisabled()
    {
        // Arrange
        var tracker = new MetricTracker("TestTopic");
        var sink = new TestSink();
        var options = new PeriodicMetricExporterOptions
        {
            IncludeZeroDeltaEntries = false
        };

        var exporter = new PeriodicMetricExporter(tracker, new[] { sink }, options);

        using (tracker.Track("Workload"))
        {
            // First run
        }

        // First harvest captures the initial run
        await exporter.HarvestAsync();
        sink.EmittedEntries.Clear();

        // Act - Harvest again without doing any new workload
        var entries = await exporter.HarvestAsync();

        // Assert - Zero activity entries are excluded
        entries.Should().BeEmpty();
        sink.EmittedEntries.Should().BeEmpty();
    }
}
