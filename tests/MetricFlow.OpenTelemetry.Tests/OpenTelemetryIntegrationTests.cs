using System.Diagnostics;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.OpenTelemetry;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace MetricFlow.OpenTelemetry.Tests;

public class OpenTelemetryIntegrationTests
{
    [Fact]
    public void OpenTelemetry_AddMetricFlowInstrumentation_ExportsMetricsToInMemoryReader()
    {
        var exportedMetrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMetricFlowInstrumentation(opt =>
            {
                opt.MeterPrefix = "DotnetKit.MetricFlow";
            })
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var tracker = new MetricTracker("OTelOrderTopic");

        // Act
        using (tracker.Track("ProcessPayment", new() { ["provider"] = "stripe" }))
        {
            Thread.Sleep(10);
        }

        meterProvider.ForceFlush();

        // Assert
        exportedMetrics.Should().NotBeEmpty();

        var durationMetric = exportedMetrics.FirstOrDefault(m => m.Name == "ProcessPayment.duration");
        durationMetric.Should().NotBeNull();
        durationMetric!.Unit.Should().Be("ms");

        var totalMetric = exportedMetrics.FirstOrDefault(m => m.Name == "ProcessPayment.total");
        totalMetric.Should().NotBeNull();
        totalMetric!.Unit.Should().Be("{operations}");

        // Inspect metric points
        var totalPoints = new List<MetricPoint>();
        foreach (ref readonly var point in totalMetric.GetMetricPoints())
        {
            totalPoints.Add(point);
        }

        totalPoints.Should().ContainSingle();
        var pointTags = ConvertMetricTags(totalPoints[0].Tags);
        pointTags["operation"].Should().Be("ProcessPayment");
        pointTags["status"].Should().Be("ok");
        pointTags["provider"].Should().Be("stripe");
    }

    [Fact]
    public void OpenTelemetry_TopicFiltering_ExportsOnlyConfiguredTopics()
    {
        var exportedMetrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMetricFlowInstrumentation(opt =>
            {
                opt.Topics.Add("AllowedTopic");
            })
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var allowedTracker = new MetricTracker("AllowedTopic");
        var ignoredTracker = new MetricTracker("IgnoredTopic");

        // Act
        using (allowedTracker.Track("AllowedOp")) { }
        using (ignoredTracker.Track("IgnoredOp")) { }

        meterProvider.ForceFlush();

        // Assert
        var metricNames = exportedMetrics.Select(m => m.Name).ToList();
        metricNames.Should().Contain(n => n.StartsWith("AllowedOp"));
        metricNames.Should().NotContain(n => n.StartsWith("IgnoredOp"));
    }

    [Fact]
    public void OpenTelemetry_ItemsThroughput_ExportsItemCounts()
    {
        var exportedMetrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMetricFlowInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var tracker = new MetricTracker("OTelBatchTopic");

        // Act
        using (var scope = tracker.Track("DataIngest"))
        {
            scope.SetItems(250);
        }

        meterProvider.ForceFlush();

        // Assert
        var itemsMetric = exportedMetrics.FirstOrDefault(m => m.Name == "DataIngest.items");
        itemsMetric.Should().NotBeNull();

        var points = new List<MetricPoint>();
        foreach (ref readonly var point in itemsMetric!.GetMetricPoints())
        {
            points.Add(point);
        }

        points.Should().ContainSingle();
        points[0].GetSumLong().Should().Be(250);
    }

    [Fact]
    public void OpenTelemetry_TracingCorrelation_EnrichesTagsWithActiveTrace()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .Build();

        var activitySource = new ActivitySource("TestSource");
        using var activity = activitySource.StartActivity("TestSpan");

        activity.Should().NotBeNull();

        var tags = new Dictionary<string, string>
        {
            ["client"] = "web"
        };

        // Act
        tags.WithTraceContext();

        // Assert
        tags.Should().ContainKey("trace_id");
        tags.Should().ContainKey("span_id");
        tags["trace_id"].Should().Be(activity!.TraceId.ToString());
        tags["span_id"].Should().Be(activity!.SpanId.ToString());
        tags["client"].Should().Be("web");
    }

    [Fact]
    public void OpenTelemetry_TracerProvider_CanBeConfigured()
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddMetricFlowInstrumentation()
            .AddInMemoryExporter(exportedActivities)
            .Build();

        // Act
        using (var activity = MetricFlowTracingExtensions.StartActivity("AuditPipeline"))
        {
            activity.Should().NotBeNull();
            activity?.SetTag("tenant", "123");
        }

        tracerProvider.ForceFlush();

        // Assert
        exportedActivities.Should().ContainSingle();
        exportedActivities[0].OperationName.Should().Be("AuditPipeline");
        exportedActivities[0].Tags.Should().Contain(t => t.Key == "tenant" && t.Value == "123");
    }

    private static Dictionary<string, string> ConvertMetricTags(in ReadOnlyTagCollection tags)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            dict[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }
        return dict;
    }
}
