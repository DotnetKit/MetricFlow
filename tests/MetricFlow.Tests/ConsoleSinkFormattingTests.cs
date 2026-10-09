using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class ConsoleSinkFormattingTests
{
    private sealed record CustomPayloadSnapshot(
        string MetricName,
        string CounterName,
        long PayloadBytes,
        DateTime Timestamp) : IMetricSnapshot
    {
        public string ToFormattedString() => $"{PayloadBytes} bytes";
    }

    [Fact]
    public void FormatDuration_WithDifferentUnits_FormatsCorrectly()
    {
        var opt = new ConsoleMetricSinkOptions();

        // Milliseconds
        opt.DurationUnit = DurationUnit.Milliseconds;
        opt.FormatDuration(TimeSpan.FromMilliseconds(1234)).Should().Be("1234.00 ms");

        // Seconds
        opt.DurationUnit = DurationUnit.Seconds;
        opt.FormatDuration(TimeSpan.FromMilliseconds(1234)).Should().Be("1.23 s");

        // Minutes
        opt.DurationUnit = DurationUnit.Minutes;
        opt.FormatDuration(TimeSpan.FromMinutes(2.5)).Should().Be("2.50 m");

        // Hours
        opt.DurationUnit = DurationUnit.Hours;
        opt.FormatDuration(TimeSpan.FromHours(1.5)).Should().Be("1.50 h");

        // Auto (< 1s => ms, < 60s => s, < 60m => m, >= 1h => h)
        opt.DurationUnit = DurationUnit.Auto;
        opt.FormatDuration(TimeSpan.FromMilliseconds(450)).Should().Be("450.00 ms");
        opt.FormatDuration(TimeSpan.FromSeconds(12.5)).Should().Be("12.50 s");
        opt.FormatDuration(TimeSpan.FromMinutes(3.2)).Should().Be("3.20 m");
        opt.FormatDuration(TimeSpan.FromHours(2.1)).Should().Be("2.10 h");
    }

    [Fact]
    public void FormatDuration_WithCustomFormatter_OverridesDurationUnit()
    {
        var opt = new ConsoleMetricSinkOptions
        {
            DurationFormatter = ts => $"{ts.TotalSeconds:F0}sec"
        };

        opt.FormatDuration(TimeSpan.FromSeconds(45)).Should().Be("45sec");
    }

    [Fact]
    public void FormatMemory_WithDifferentUnits_FormatsCorrectly()
    {
        var opt = new ConsoleMetricSinkOptions();

        // Bytes
        opt.MemoryUnit = MemoryUnit.Bytes;
        opt.FormatMemory(1048576).Should().Be("1048576 B");

        // Kilobytes
        opt.MemoryUnit = MemoryUnit.Kilobytes;
        opt.FormatMemory(1048576).Should().Be("1024.00 KB");

        // Megabytes
        opt.MemoryUnit = MemoryUnit.Megabytes;
        opt.FormatMemory(1048576).Should().Be("1.00 MB");

        // Gigabytes
        opt.MemoryUnit = MemoryUnit.Gigabytes;
        opt.FormatMemory(1073741824).Should().Be("1.00 GB");

        // Auto
        opt.MemoryUnit = MemoryUnit.Auto;
        opt.FormatMemory(512).Should().Be("512 B");
        opt.FormatMemory(64 * 1024).Should().Be("64.00 KB");
        opt.FormatMemory(32 * 1024 * 1024).Should().Be("32.00 MB");
        opt.FormatMemory(2L * 1024 * 1024 * 1024).Should().Be("2.00 GB");
    }

    [Fact]
    public void ThroughputUnit_CustomValue_RenderedInOutput()
    {
        using var stringWriter = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = false,
            ThroughputUnit = "req/s",
            Prefix = ""
        };
        var sink = new ConsoleMetricSink(opt);

        var snap = new ThroughputSnapshot(
            MetricName: "HttpRequests",
            CounterName: "Throughput",
            TotalItems: 100,
            TotalOperations: 10,
            TotalDuration: TimeSpan.FromSeconds(2),
            ItemsPerSecond: 50.5,
            AverageItemsPerOperation: 10.0,
            Timestamp: DateTime.UtcNow,
            FailedOperations: 0);

        sink.Emit([snap]);

        var output = stringWriter.ToString();
        output.Should().Contain("50.5 req/s");
    }

    [Fact]
    public void CustomFormatter_ByType_FormatsCustomSnapshot()
    {
        using var stringWriter = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = false,
            Prefix = ""
        };

        opt.AddFormatter<CustomPayloadSnapshot>(p => $"CustomPayload: {p.PayloadBytes / 1024} KB transferred");

        var sink = new ConsoleMetricSink(opt);

        var snap = new CustomPayloadSnapshot("UploadFile", "Payload", 20480, DateTime.UtcNow);

        sink.Emit([snap]);

        var output = stringWriter.ToString();
        output.Should().Contain("[Payload:UploadFile] CustomPayload: 20 KB transferred");
    }

    [Fact]
    public void CustomFormatter_ByName_FormatsMatchingCounterSnapshot()
    {
        using var stringWriter = new StringWriter();
        var opt = new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = false,
            Prefix = ""
        };

        opt.AddFormatter("SpecialThroughput", s => $"Special rate: {s.MetricName}");

        var sink = new ConsoleMetricSink(opt);

        var snap = new ThroughputSnapshot(
            MetricName: "OrderStream",
            CounterName: "SpecialThroughput",
            TotalItems: 10,
            TotalOperations: 1,
            TotalDuration: TimeSpan.FromSeconds(1),
            ItemsPerSecond: 10.0,
            AverageItemsPerOperation: 10.0,
            Timestamp: DateTime.UtcNow,
            FailedOperations: 0);

        sink.Emit([snap]);

        var output = stringWriter.ToString();
        output.Should().Contain("[SpecialThroughput:OrderStream] Special rate: OrderStream");
    }
}
