using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class ConsoleSinkTableModeTests
{
    [Fact]
    public void Emit_InTableMode_RendersFormattedColumns()
    {
        using var writer = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            DisplayMode = ConsoleDisplayMode.Table,
            Colorize = false,
            Prefix = string.Empty,
            IncludeTimestamp = false
        };

        var sink = new ConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                MetricName: "Stage1",
                CounterName: "Duration",
                InCount: 100,
                OutCount: 100,
                FailedCount: 0,
                TotalDuration: TimeSpan.FromMilliseconds(1250),
                AverageDuration: TimeSpan.FromMilliseconds(12.5),
                MinDuration: TimeSpan.FromMilliseconds(2),
                MaxDuration: TimeSpan.FromMilliseconds(30),
                Timestamp: now),
            new ThroughputSnapshot("Stage1", "Throughput", 5000, 100, TimeSpan.FromSeconds(12.5), 400.0, 50.0, now),
            new MemorySnapshot("Stage1", "Memory", 100, 10240, 102, 100, 105, now)
        };

        sink.Emit(snapshots);

        var output = writer.ToString();
        output.Should().Contain("Operation");
        output.Should().Contain("Operations");
        output.Should().Contain("Duration (Avg / p99)");
        output.Should().Contain("Throughput Rate");
        output.Should().Contain("Memory (Avg / Total)");
        output.Should().Contain("Stage1");
        output.Should().Contain("100");
        output.Should().Contain("12.50 ms");
        output.Should().Contain("400.0 items/s");
    }

    [Fact]
    public void Emit_InTableMode_WithCellThresholds_AppliesAnsiColorToSpecificCellOnly()
    {
        using var writer = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            DisplayMode = ConsoleDisplayMode.Table,
            Colorize = true,
            Prefix = string.Empty,
            IncludeTimestamp = false
        };

        // Add duration threshold: warn at 10ms, critical at 50ms
        opt.AddThreshold<DurationSnapshot>(
            warn: TimeSpan.FromMilliseconds(10),
            critical: TimeSpan.FromMilliseconds(50),
            warnColor: ConsoleColor.Yellow,
            criticalColor: ConsoleColor.Red
        );

        var sink = new ConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                MetricName: "StageCritical",
                CounterName: "Duration",
                InCount: 50,
                OutCount: 50,
                FailedCount: 0,
                TotalDuration: TimeSpan.FromMilliseconds(1250),
                AverageDuration: TimeSpan.FromMilliseconds(25),
                MinDuration: TimeSpan.FromMilliseconds(5),
                MaxDuration: TimeSpan.FromMilliseconds(35),
                Timestamp: now),
            new ThroughputSnapshot("StageCritical", "Throughput", 500, 50, TimeSpan.FromSeconds(5), 100.0, 10.0, now)
        };

        sink.Emit(snapshots);

        var output = writer.ToString();

        // ANSI Yellow code (\u001b[33m) should be present around the duration cell
        output.Should().Contain("\u001b[33m");
        output.Should().Contain("25.00 ms");

        // Stage column text should NOT be colored with Yellow
        output.Should().NotContain("\u001b[33mStageCritical");
    }

    [Fact]
    public void Emit_InTableMode_RendersDimensionSubRows()
    {
        using var writer = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            DisplayMode = ConsoleDisplayMode.Table,
            Colorize = false,
            ShowDimensionBreakdown = true,
            Prefix = string.Empty,
            IncludeTimestamp = false
        };

        var sink = new ConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var dimSnapshot = new DimensionSnapshot(
            MetricName: "EnrichStage",
            CounterName: "Dimension",
            DimensionName: "sensor_id",
            TotalOperations: 100,
            TaggedOperations: 100,
            UntaggedOperations: 0,
            FailedOperations: 0,
            Breakdown: new Dictionary<string, long>
            {
                ["sensor-west-01"] = 60,
                ["sensor-east-02"] = 40
            },
            Timestamp: now
        );

        sink.Emit([dimSnapshot]);

        var output = writer.ToString();
        output.Should().Contain("EnrichStage");
        output.Should().Contain("└─ sensor_id: sensor-west-01 (60.0%), sensor-east-02 (40.0%)");
    }

    [Fact]
    public void Emit_InMarkdownMode_RendersMarkdownTable()
    {
        using var writer = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            DisplayMode = ConsoleDisplayMode.Markdown,
            Prefix = string.Empty,
            IncludeTimestamp = false
        };

        var sink = new ConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot(
                MetricName: "PipelineStage",
                CounterName: "Duration",
                InCount: 50,
                OutCount: 50,
                FailedCount: 0,
                TotalDuration: TimeSpan.FromMilliseconds(500),
                AverageDuration: TimeSpan.FromMilliseconds(10),
                MinDuration: TimeSpan.FromMilliseconds(5),
                MaxDuration: TimeSpan.FromMilliseconds(20),
                Timestamp: now)
        };

        sink.Emit(snapshots);

        var output = writer.ToString();
        output.Should().Contain("| Operation | Operations | Duration (Avg)");
        output.Should().Contain("| :--- | ---: |");
        output.Should().Contain("| PipelineStage | 50 | 10.00 ms");
    }

    [Fact]
    public void Emit_InFlatLogMode_PreservesOriginalBehavior()
    {
        using var writer = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = writer,
            DisplayMode = ConsoleDisplayMode.FlatLog,
            Colorize = false,
            Prefix = "[TestPrefix]",
            IncludeTimestamp = false
        };

        var sink = new ConsoleMetricSink(opt);
        var now = DateTime.UtcNow;

        var snapshots = new IMetricSnapshot[]
        {
            new DurationSnapshot("TestOp", "Duration", 1, 1, 0, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5), now)
        };

        sink.Emit(snapshots);

        var output = writer.ToString();
        output.Should().Contain("[TestPrefix] [Duration:TestOp] Avg: 5.00 ms");
    }
}
