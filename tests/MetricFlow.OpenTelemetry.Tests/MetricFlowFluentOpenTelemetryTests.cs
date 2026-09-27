using System.Diagnostics;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.OpenTelemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace MetricFlow.OpenTelemetry.Tests;

public class MetricFlowFluentOpenTelemetryTests
{
    [Fact]
    public void MetricFlowTelemetry_CreateMeterProvider_ExportsMetricsProperly()
    {
        // Arrange
        var exportedMetrics = new List<Metric>();

        using var meterProvider = MetricFlowTelemetry.CreateMeterProvider(
            configure: metrics =>
            {
                metrics.AddInMemoryExporter(exportedMetrics);
            },
            configureMetricFlow: opt =>
            {
                opt.MeterPrefix = "DotnetKit.MetricFlow";
            });

        var tracker = new MetricTracker("OrdersTopic");

        // Act
        using (tracker.Track("Checkout", new() { ["currency"] = "USD" }))
        {
            Thread.Sleep(5);
        }

        meterProvider.ForceFlush();

        // Assert
        exportedMetrics.Should().NotBeEmpty();
        exportedMetrics.Should().Contain(m => m.Name == "Checkout.duration");
        exportedMetrics.Should().Contain(m => m.Name == "Checkout.total");
    }

    [Fact]
    public void MetricFlowTelemetry_CreateMeterProviderBuilder_ReturnsConfiguredBuilder()
    {
        // Arrange
        var exportedMetrics = new List<Metric>();

        using var meterProvider = MetricFlowTelemetry.CreateMeterProviderBuilder()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var tracker = new MetricTracker("PaymentsTopic");

        // Act
        using (tracker.Track("AuthorizeCard"))
        {
            Thread.Sleep(5);
        }

        meterProvider.ForceFlush();

        // Assert
        exportedMetrics.Should().NotBeEmpty();
        exportedMetrics.Should().Contain(m => m.Name == "AuthorizeCard.total");
    }

    [Fact]
    public void ServiceCollection_WithOpenTelemetry_RegistersMetricsAndTracingFluently()
    {
        // Arrange
        var services = new ServiceCollection();
        var exportedMetrics = new List<Metric>();
        var exportedActivities = new List<Activity>();

        // Act - fluent chain: AddMetricFlow -> WithOpenTelemetry -> AddMetricTracker
        var builder = services.AddMetricFlow("CoreService", opt => opt.AddThroughputCounter())
            .WithOpenTelemetry(otel =>
            {
                otel.WithMetrics(m => m.AddInMemoryExporter(exportedMetrics));
                otel.WithTracing(t => t.AddInMemoryExporter(exportedActivities));
                otel.ConfigureInstrumentation(opt => opt.RecordActiveOperations = true);
            })
            .AddMetricTracker("AuxiliaryService");

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeAssignableTo<IMetricFlowBuilder>();

        using var sp = services.BuildServiceProvider();
        var tracker = sp.GetRequiredService<IMetricTracker>();
        tracker.Should().NotBeNull();
        tracker.Topic.Should().Be("CoreService");

        // Force resolution of MeterProvider and TracerProvider to verify DI wiring
        var meterProvider = sp.GetService<MeterProvider>();
        var tracerProvider = sp.GetService<TracerProvider>();

        meterProvider.Should().NotBeNull();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollection_WithOpenTelemetry_DefaultOverload_RegistersMetricsByDefault()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMetricFlow("DefaultTopic")
            .WithOpenTelemetry();

        using var sp = services.BuildServiceProvider();

        // Assert
        var meterProvider = sp.GetService<MeterProvider>();
        meterProvider.Should().NotBeNull();
    }
}
