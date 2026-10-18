using System.Text.Json;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Exporters;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class MetricExportExtensionsTests
{
    private static IReadOnlyList<IMetricSnapshot> CreateSampleSnapshots()
    {
        var now = DateTime.UtcNow;
        return
        [
            new DurationSnapshot(
                MetricName: "Stage1_Extract",
                CounterName: "Duration",
                InCount: 100,
                OutCount: 100,
                FailedCount: 1,
                TotalDuration: TimeSpan.FromMilliseconds(2000),
                AverageDuration: TimeSpan.FromMilliseconds(20),
                MinDuration: TimeSpan.FromMilliseconds(5),
                MaxDuration: TimeSpan.FromMilliseconds(60),
                Timestamp: now,
                P95Duration: TimeSpan.FromMilliseconds(45),
                P99Duration: TimeSpan.FromMilliseconds(55)),
            new ThroughputSnapshot("Stage1_Extract", "Throughput", 10000, 100, TimeSpan.FromSeconds(20), 500.0, 100.0, now),
            new MemorySnapshot("Stage1_Extract", "Memory", 100, 204800, 2048, 1024, 4096, now),
            new FailureSnapshot("Stage1_Extract", "Failure", 100, 1, 1, 0, now),
            new DimensionSnapshot("Stage1_Extract", "Dimension", "region", 100, 100, 0, 1, new Dictionary<string, long> { ["us-east"] = 70, ["eu-west"] = 30 }, now)
        ];
    }

    [Fact]
    public void ToMarkdownTable_ProducesValidMarkdown()
    {
        var snapshots = CreateSampleSnapshots();
        var md = snapshots.ToMarkdownTable(new MarkdownTableOptions { Title = "ETL Pipeline Metrics" });

        md.Should().Contain("### ETL Pipeline Metrics");
        md.Should().Contain("| Operation | Operations | Duration (Avg)");
        md.Should().Contain("| Stage1_Extract | 100 | 20.00 ms");
        md.Should().Contain("500.0 items/s");
        md.Should().Contain("us-east (70.0%), eu-west (30.0%)");
    }

    [Fact]
    public void ToCsv_ProducesValidRFC4180Csv()
    {
        var snapshots = CreateSampleSnapshots();
        var csv = snapshots.ToCsv();

        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2); // Header + 1 Operation row

        var header = lines[0];
        header.Should().StartWith("Operation,TotalOperations,DurationAvgMs");

        var row = lines[1];
        row.Should().StartWith("Stage1_Extract,100,20.000");
        row.Should().Contain("500.00");
    }

    [Fact]
    public void ToJson_ProducesValidDeserializableJson()
    {
        var snapshots = CreateSampleSnapshots();
        var json = snapshots.ToJson(new JsonExportOptions { Indented = true });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("totalOperations").GetInt32().Should().Be(1);
        var ops = root.GetProperty("operations");
        ops.GetArrayLength().Should().Be(1);

        var s1 = ops[0];
        s1.GetProperty("operationName").GetString().Should().Be("Stage1_Extract");
        s1.GetProperty("totalOperations").GetInt64().Should().Be(100);
        s1.GetProperty("averageDurationMs").GetDouble().Should().Be(20.0);
        s1.GetProperty("itemsPerSecond").GetDouble().Should().Be(500.0);
        s1.GetProperty("dimensions").GetProperty("region").GetProperty("us-east").GetInt64().Should().Be(70);
    }

    [Fact]
    public void ToMarkdownTable_FromTracker_WorksSeamlessly()
    {
        using var tracker = new MetricTracker("ExportTopic")
            .AddThroughputCounter();

        using (tracker.TrackItems("QueryDatabase", 25))
        {
            // simulated op
        }

        var md = tracker.ToMarkdownTable();
        md.Should().Contain("QueryDatabase");
        md.Should().Contain("items/s");
    }
}
