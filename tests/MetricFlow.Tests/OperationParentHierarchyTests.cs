using System.Collections.Concurrent;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Exporters;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class OperationParentHierarchyTests
{
    [Fact]
    public async Task NestedTrackAndInOut_AutomaticallyCorrelateParentViaAsyncLocalWithoutHierarchyCounter()
    {
        // Arrange - No HierarchyCounter, no manual "parent" metadata passed
        var spy = new ContextSpyCounter("Spy");
        using var tracker = new MetricTracker("AsyncFlow").AddDurationCounter();
        tracker.RegisterCounter(spy);

        // Act
        tracker.In("RootJob");

        await Task.WhenAll(
            Task.Run(async () =>
            {
                using (tracker.Track("StepA", tags: new() { ["region"] = "EU" }))
                {
                    await Task.Delay(5);
                    using (tracker.Track("StepA_Sub"))
                    {
                        await Task.Delay(2);
                    }
                }
            }),
            Task.Run(async () =>
            {
                using (tracker.Track("StepB"))
                {
                    await Task.Delay(5);
                }
            })
        );

        tracker.Out("RootJob");

        // Subsequent top-level operation must NOT inherit RootJob as parent
        using (tracker.Track("IndependentOp")) { }

        // Assert
        tracker.GetParentOperation("RootJob").Should().BeNull();
        tracker.GetParentOperation("StepA").Should().Be("RootJob");
        tracker.GetParentOperation("StepA_Sub").Should().Be("StepA");
        tracker.GetParentOperation("StepB").Should().Be("RootJob");
        tracker.GetParentOperation("IndependentOp").Should().BeNull();

        // Ambient parent is stored in metadata[MetricFlowMetadataKeys.Parent] without polluting business tags
        spy.CapturedTags["StepA"].Should().ContainKey("region");
        spy.CapturedTags["StepA"].Should().NotContainKey(MetricFlowMetadataKeys.Parent);
        spy.CapturedMetadata["StepA"].Should().ContainKey(MetricFlowMetadataKeys.Parent).WhoseValue.Should().Be("RootJob");
        spy.CapturedMetadata["StepA_Sub"].Should().ContainKey(MetricFlowMetadataKeys.Parent).WhoseValue.Should().Be("StepA");
        spy.CapturedMetadata["IndependentOp"].Should().BeNull();

        var summaries = tracker.GetOperationSummaries();
        summaries.First(s => s.OperationName == "RootJob").Depth.Should().Be(0);
        summaries.First(s => s.OperationName == "StepA").Depth.Should().Be(1);
        summaries.First(s => s.OperationName == "StepA_Sub").Depth.Should().Be(2);
        summaries.First(s => s.OperationName == "StepB").Depth.Should().Be(1);
        summaries.First(s => s.OperationName == "IndependentOp").Depth.Should().Be(0);
    }

    [Fact]
    public void ParentMetadataAndSetParent_BuildHierarchyWithoutPollutingBusinessTags()
    {
        // Arrange - No HierarchyCounter registered
        var spy = new ContextSpyCounter("Spy");
        using var tracker = new MetricTracker("Orders")
            .AddDurationCounter()
            .AddDimensionCounter(MetricFlowMetadataKeys.Parent);
        tracker.RegisterCounter(spy);

        // Act
        using (tracker.Track("Checkout"))
        {
            using (tracker.Track(
                "ValidateCart",
                tags: new() { ["region"] = "EU" },
                metadata: new() { [MetricFlowMetadataKeys.Parent] = "Checkout", ["trace_id"] = "trace-123" })) { }

            using (var paymentScope = tracker.Track("ChargePayment", tags: new() { ["region"] = "US" }).SetParent("Checkout"))
            {
                _ = paymentScope;
                using (tracker.Track("GatewayCall", metadata: new() { [MetricFlowMetadataKeys.Parent] = "ChargePayment" })) { }
            }
        }

        // Assert - Summaries are ordered hierarchically with ParentOperation & Depth populated
        var summaries = tracker.GetOperationSummaries();
        summaries.Select(s => s.OperationName).Should().Equal(
            "Checkout",
            "ValidateCart",
            "ChargePayment",
            "GatewayCall"
        );

        summaries[0].ParentOperation.Should().BeNull();
        summaries[0].Depth.Should().Be(0);

        summaries[1].ParentOperation.Should().Be("Checkout");
        summaries[1].Depth.Should().Be(1);

        summaries[2].ParentOperation.Should().Be("Checkout");
        summaries[2].Depth.Should().Be(1);

        summaries[3].ParentOperation.Should().Be("ChargePayment");
        summaries[3].Depth.Should().Be(2);

        // Single-operation summary also includes ParentOperation
        var single = tracker.GetOperationSummary("GatewayCall");
        single.Should().NotBeNull();
        single.ParentOperation.Should().Be("ChargePayment");

        // Parent is stored in metadata, NOT in business tags
        spy.CapturedTags["ValidateCart"].Should().ContainKey("region");
        spy.CapturedTags["ValidateCart"].Should().NotContainKey(MetricFlowMetadataKeys.Parent);
        spy.CapturedMetadata["ValidateCart"].Should().ContainKey(MetricFlowMetadataKeys.Parent).WhoseValue.Should().Be("Checkout");
        spy.CapturedMetadata["ValidateCart"].Should().ContainKey("trace_id").WhoseValue.Should().Be("trace-123");

        spy.CapturedTags["ChargePayment"].Should().ContainKey("region");
        spy.CapturedTags["ChargePayment"].Should().NotContainKey(MetricFlowMetadataKeys.Parent);
        spy.CapturedMetadata["ChargePayment"].Should().ContainKey(MetricFlowMetadataKeys.Parent).WhoseValue.Should().Be("Checkout");

        // Parent metadata is also visible to DimensionCounter
        var parentDim = tracker.GetDimensionSnapshot("ValidateCart", MetricFlowMetadataKeys.Parent);
        parentDim.Should().NotBeNull();
        parentDim.Breakdown["Checkout"].Should().Be(1);
    }

    [Fact]
    public void ParentMetadata_RendersIndentedRowsInMarkdownAndConsoleTable()
    {
        // Arrange
        using var tracker = new MetricTracker("Pipeline").AddDurationCounter();
        using (tracker.Track("RunJob"))
        {
            using (tracker.Track("Extract", metadata: new() { [MetricFlowMetadataKeys.Parent] = "RunJob" })) { }
            using (tracker.Track("Load").SetParent("RunJob")) { }
        }

        // Act
        var markdown = tracker.ToMarkdownTable();
        var table = TableMetricFormatter.RenderTable(
            tracker.GetOperationSummaries(),
            new ConsoleMetricSinkOptions { Colorize = false }
        );

        // Assert
        markdown.Should().Contain("| RunJob |");
        markdown.Should().Contain("| └─ Extract |");
        markdown.Should().Contain("| └─ Load |");

        table.Should().Contain("RunJob");
        table.Should().Contain("└─ Extract");
        table.Should().Contain("└─ Load");
    }

    [Fact]
    public async Task ConsoleSink_TreeDisplayMode_RendersVisualParentChildTreeWithSelfMetrics()
    {
        // Arrange
        using var writer = new StringWriter();
        using var tracker = new MetricTracker("MediaCatalog")
            .AddTelemetryCounters(includeMemory: true)
            .AddThroughputCounter()
            .AddConsoleSink(opt =>
            {
                opt.OutputWriter = writer;
                opt.Colorize = false;
                opt.DisplayMode = ConsoleDisplayMode.Tree;
            });

        // Act
        using (tracker.Track("GetChannelProgramsUseCase"))
        {
            await Task.Delay(15);
            using (var step1 = tracker.Track("Adapter.MatchSingleChannelUseCase"))
            {
                step1.SetItems(1);
                await Task.Delay(5);
            }

            using (var step2 = tracker.Track("Adapter.GetProgrammesForChannel"))
            {
                step2.SetItems(1450);
                await Task.Delay(10);

                using (tracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(5);
                }

                using (tracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(5);
                }
            }
        }

        await tracker.FlushSinksAsync();
        var output = writer.ToString();

        // Assert
        output.Should().Contain("▼ [GetChannelProgramsUseCase]");
        output.Should().Contain("self:");
        output.Should().Contain("  ├─ [Adapter.MatchSingleChannelUseCase]");
        output.Should().Contain("  └─ [Adapter.GetProgrammesForChannel]");
        output.Should().Contain("      └─ [TableStorage.QuerySegmentAsync] x2");
        output.Should().Contain("avg:");
        output.Should().Contain("items: 1,450");
    }

    [Fact]
    public void ParentMetadata_WithCyclesOrMissingParent_DoesNotInfiniteLoop()
    {
        // Arrange
        using var tracker = new MetricTracker("Cyclic").AddDurationCounter();
        using (tracker.Track("OpA", metadata: new() { [MetricFlowMetadataKeys.Parent] = "OpB" })) { }
        using (tracker.Track("OpB", metadata: new() { [MetricFlowMetadataKeys.Parent] = "OpA" })) { }
        using (tracker.Track("Orphan", metadata: new() { [MetricFlowMetadataKeys.Parent] = "DoesNotExist" })) { }

        // Act
        var summaries = tracker.GetOperationSummaries();

        // Assert
        summaries.Should().HaveCount(3);
        summaries.Select(s => s.OperationName).Should().Contain(["OpA", "OpB", "Orphan"]);
    }

    [Fact]
    public async Task ConsoleDisplayModeTree_RendersVisualParentChildTreeWithSelfDurationAndItems()
    {
        // Arrange
        using var writer = new StringWriter();
        using var tracker = new MetricTracker("MediaCatalog")
            .AddTelemetryCounters(includeMemory: true)
            .AddThroughputCounter()
            .AddConsoleSink(opt =>
            {
                opt.OutputWriter = writer;
                opt.Colorize = false;
                opt.IncludeTimestamp = false;
                opt.Prefix = "[Catalog:Trace]";
                opt.DisplayMode = ConsoleDisplayMode.Tree;
            });

        // Act
        using (tracker.Track("GetChannelProgramsUseCase"))
        {
            await Task.Delay(10);

            using (var step1 = tracker.Track("Adapter.MatchSingleChannelUseCase"))
            {
                step1.SetItems(1);
                await Task.Delay(5);
            }

            using (var step2 = tracker.Track("Adapter.GetProgrammesForChannel"))
            {
                step2.SetItems(1450);
                await Task.Delay(10);

                using (tracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(5);
                }

                using (tracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(5);
                }
            }
        }

        await tracker.FlushSinksAsync();
        var output = writer.ToString();

        // Assert
        output.Should().Contain("[Catalog:Trace]");
        output.Should().Contain("▼ [GetChannelProgramsUseCase]");
        output.Should().Contain("self:");
        output.Should().Contain("├─ [Adapter.MatchSingleChannelUseCase]");
        output.Should().Contain("items: 1");
        output.Should().Contain("└─ [Adapter.GetProgrammesForChannel]");
        output.Should().Contain("items: 1,450");
        output.Should().Contain("└─ [TableStorage.QuerySegmentAsync] x2");
        output.Should().Contain("avg:");
    }

    private sealed class ContextSpyCounter(string name) : CounterBase(name)
    {
        public ConcurrentDictionary<string, IReadOnlyDictionary<string, string>?> CapturedTags { get; } = [];
        public ConcurrentDictionary<string, IReadOnlyDictionary<string, string>?> CapturedMetadata { get; } = [];

        public override object? OnIn(in InContext context) => null;

        public override void OnOut(object? state, in OutContext context)
        {
            CapturedTags[context.MetricName] = context.Tags;
            CapturedMetadata[context.MetricName] = context.Metadata;
        }

        public override IMetricSnapshot? GetSnapshot(string metricName) => null;
        public override IEnumerable<IMetricSnapshot> GetAllSnapshots() => [];
        public override void Reset()
        {
            CapturedTags.Clear();
            CapturedMetadata.Clear();
        }
    }
}
