using System.Diagnostics;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Hierarchy;
using DotnetKit.MetricFlow.Sinks.Console;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class HierarchyCounterTests
{
    [Fact]
    public async Task HierarchyCounter_CorrelatesParentAndChildOperations()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Orders"
        }.AddHierarchyCounter());

        // Act
        using (tracker.Track("GetChannelProgramsUseCase"))
        {
            await Task.Delay(20);

            using (tracker.Track("MatchSingleChannelUseCase"))
            {
                await Task.Delay(10);
            }

            using (tracker.Track("TableStorage.GetProgrammesForChannel"))
            {
                await Task.Delay(15);

                using (tracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(5);
                }
            }
        }

        // Assert
        var tree = tracker.GetHierarchySnapshot("GetChannelProgramsUseCase");
        tree.Should().NotBeNull();
        tree!.MetricName.Should().Be("GetChannelProgramsUseCase");
        tree.Root.MetricName.Should().Be("GetChannelProgramsUseCase");
        tree.Root.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(20));

        // Two direct children
        tree.Root.Children.Should().HaveCount(2);
        var child1 = tree.Root.Children[0];
        child1.MetricName.Should().Be("MatchSingleChannelUseCase");
        child1.Children.Should().BeEmpty();
        child1.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(5));

        var child2 = tree.Root.Children[1];
        child2.MetricName.Should().Be("TableStorage.GetProgrammesForChannel");
        child2.Children.Should().HaveCount(1);
        child2.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(10));

        var grandChild = child2.Children[0];
        grandChild.MetricName.Should().Be("TableStorage.QuerySegmentAsync");
        grandChild.Children.Should().BeEmpty();

        // Self-duration calculations
        tree.Root.SelfDuration.Should().BeLessThan(tree.Root.Duration);
        child2.SelfDuration.Should().BeLessThan(child2.Duration);
    }

    [Fact]
    public void HierarchyCounter_TracksItemsAndAllocations()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Catalog"
        }.AddHierarchyCounter());

        // Act
        using (tracker.Track("RootOp"))
        {
            using (var childScope = tracker.Track("ChildOp"))
            {
                childScope.SetItems(500);
                // Allocate some memory
                _ = new byte[128 * 1024];
            }
        }

        // Assert
        var tree = tracker.GetHierarchySnapshot("RootOp");
        tree.Should().NotBeNull();
        tree!.Root.Children.Should().HaveCount(1);

        var child = tree.Root.Children[0];
        child.ItemCount.Should().Be(500);
        child.AllocatedBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HierarchyCounter_WhenChildFails_RecordsExceptionAndFailure()
    {
        // Arrange
        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "Checkout"
        }.AddHierarchyCounter());

        // Act
        using (tracker.Track("OrderCheckoutUseCase"))
        {
            try
            {
                using var childScope = tracker.Track("PaymentService.ChargeCard");
                childScope.SetException(new TimeoutException("Gateway timeout"));
                throw new TimeoutException("Gateway timeout");
            }
            catch (TimeoutException)
            {
                // Handled in root
            }
        }

        // Assert
        var tree = tracker.GetHierarchySnapshot("OrderCheckoutUseCase");
        tree.Should().NotBeNull();
        tree!.Root.Failed.Should().BeFalse(); // Root itself completed successfully

        var child = tree.Root.Children[0];
        child.MetricName.Should().Be("PaymentService.ChargeCard");
        child.Failed.Should().BeTrue();
        child.Exception.Should().BeOfType<TimeoutException>();
        child.Exception!.Message.Should().Be("Gateway timeout");
    }

    [Fact]
    public void HierarchyTreeSnapshot_ToFormattedString_RendersTreeStructure()
    {
        // Arrange
        var root = new HierarchyNode("Parent");
        root.Duration = TimeSpan.FromMilliseconds(100);
        root.SelfDuration = TimeSpan.FromMilliseconds(20);

        var child1 = new HierarchyNode("Child1", root);
        child1.Duration = TimeSpan.FromMilliseconds(30);
        child1.ItemCount = 10;
        root.AddChild(child1);

        var child2 = new HierarchyNode("Child2", root);
        child2.Duration = TimeSpan.FromMilliseconds(50);
        child2.SelfDuration = TimeSpan.FromMilliseconds(10);
        root.AddChild(child2);

        var grandChild = new HierarchyNode("SubChild", child2);
        grandChild.Duration = TimeSpan.FromMilliseconds(40);
        child2.AddChild(grandChild);

        var snapshot = new HierarchyTreeSnapshot("Parent", "Hierarchy", root, DateTime.UtcNow);

        // Act
        var formatted = snapshot.ToFormattedString();

        // Assert
        formatted.Should().Contain("▼ [Parent]");
        formatted.Should().Contain("├─ [Child1]");
        formatted.Should().Contain("items: 10");
        formatted.Should().Contain("└─ [Child2]");
        formatted.Should().Contain("└─ [SubChild]");
    }

    [Fact]
    public void ConsoleMetricSink_WithHierarchySnapshot_RendersTree()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        var sink = new ConsoleMetricSink(new ConsoleMetricSinkOptions
        {
            OutputWriter = stringWriter,
            Colorize = false,
            IncludeTimestamp = false,
            Prefix = ""
        });

        var root = new HierarchyNode("RootProcess");
        root.Duration = TimeSpan.FromMilliseconds(250);
        var child = new HierarchyNode("StepA", root);
        child.Duration = TimeSpan.FromMilliseconds(100);
        root.AddChild(child);

        var snapshot = new HierarchyTreeSnapshot("RootProcess", "Hierarchy", root, DateTime.UtcNow);

        // Act
        sink.Emit([snapshot]);

        // Assert
        var output = stringWriter.ToString();
        output.Should().Contain("[Hierarchy:RootProcess]");
        output.Should().Contain("▼ [RootProcess]");
        output.Should().Contain("└─ [StepA]");
    }

    [Fact]
    public void HierarchyCounter_WhenActivityListenerActive_StartsCorrelatedActivities()
    {
        // Arrange
        var startedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "DotnetKit.MetricFlow",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => startedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var tracker = new MetricTracker(new MetricFlowOptions
        {
            Topic = "TracingTest"
        }.AddHierarchyCounter());

        // Act
        using (tracker.Track("ParentActivity"))
        {
            using (tracker.Track("ChildActivity"))
            {
            }
        }

        // Assert
        startedActivities.Should().Contain(a => a.OperationName == "ParentActivity");
        startedActivities.Should().Contain(a => a.OperationName == "ChildActivity");

        var parent = startedActivities.First(a => a.OperationName == "ParentActivity");
        var child = startedActivities.First(a => a.OperationName == "ChildActivity");

        child.ParentId.Should().Be(parent.Id);
    }
}
