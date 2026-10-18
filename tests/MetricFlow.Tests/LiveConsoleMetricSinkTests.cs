using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class LiveConsoleMetricSinkTests
{
    [Fact]
    public void Emit_UpdatesDashboardStateAndRendersTable()
    {
        using var writer = new StringWriter();
        var opt = new LiveConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            RefreshInterval = TimeSpan.Zero, // Disable automatic background tick during test
            Colorize = false,
            Title = "Test Pipeline"
        };

        using var sink = new LiveConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                MetricName: "StageLive",
                CounterName: "Duration",
                InCount: 10,
                OutCount: 10,
                FailedCount: 0,
                TotalDuration: TimeSpan.FromMilliseconds(50),
                AverageDuration: TimeSpan.FromMilliseconds(5),
                MinDuration: TimeSpan.FromMilliseconds(1),
                MaxDuration: TimeSpan.FromMilliseconds(10),
                Timestamp: now),
            new ThroughputSnapshot("StageLive", "Throughput", 200, 10, TimeSpan.FromSeconds(2), 100.0, 20.0, now)
        };

        sink.Emit(snapshots);
        sink.RenderDashboard();

        var output = writer.ToString();
        output.Should().Contain("Test Pipeline");
        output.Should().Contain("StageLive");
        output.Should().Contain("10");
        output.Should().Contain("5.00 ms");
        output.Should().Contain("100.0 items/s");
    }

    [Fact]
    public void Tracker_WithAddLiveConsoleDashboard_CapturesExecutions()
    {
        using var writer = new StringWriter();
        using var tracker = new MetricTracker("LivePipeline")
            .AddThroughputCounter()
            .AddLiveConsoleDashboard(opt =>
            {
                opt.OutputWriter = writer;
                opt.RefreshInterval = TimeSpan.Zero;
                opt.Colorize = false;
            });

        using (tracker.TrackItems("IngestBatch", 150))
        {
            // simulated op
        }

        // Flush snapshots to sinks
        tracker.FlushSinks();

        // Find sink and manually render
        var liveSink = tracker.GetSinks().OfType<LiveConsoleMetricSink>().FirstOrDefault();
        liveSink.Should().NotBeNull();
        liveSink!.RenderDashboard();

        var output = writer.ToString();
        output.Should().Contain("IngestBatch");
        output.Should().Contain("items/s");
    }

    [Fact]
    public async Task DisposeAsync_CompletesGracefullyWithoutThrowing()
    {
        using var writer = new StringWriter();
        var opt = new LiveConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            RefreshInterval = TimeSpan.FromMilliseconds(50),
            Colorize = false
        };

        var sink = new LiveConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        sink.Emit([new DurationSnapshot("Stage1", "Duration", 1, 1, 0, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), now)]);

        await Task.Delay(100);
        await sink.DisposeAsync();

        writer.ToString().Should().Contain("Stage1");
    }
}
