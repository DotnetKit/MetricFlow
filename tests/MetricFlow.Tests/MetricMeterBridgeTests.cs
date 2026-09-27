using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Extensions;
using DotnetKit.MetricFlow.Meters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MetricFlow.Tests;

public class MetricMeterBridgeTests
{
    [Fact]
    public void StandardMeterListener_TrackOperation_PublishesDurationHistogramAndTotalCount()
    {
        // Acceptance Criterion 1: Tests assert that tracker.Track("Checkout") publishes
        // a duration histogram record and an incremented count to a listener subscribed to DotnetKit.MetricFlow.*

        var durationRecords = new ConcurrentBag<(string InstrumentName, double Value, Dictionary<string, string> Tags)>();
        var totalRecords = new ConcurrentBag<(string InstrumentName, long Value, Dictionary<string, string> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.CheckoutTopic")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            var tagDict = ConvertTags(tags);
            durationRecords.Add((instrument.Name, measurement, tagDict));
        });

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagDict = ConvertTags(tags);
            if (instrument.Name.EndsWith(".total"))
            {
                totalRecords.Add((instrument.Name, measurement, tagDict));
            }
        });

        listener.Start();

        var tracker = new MetricTracker("CheckoutTopic");

        // Act
        using (tracker.Track("Checkout", new() { ["region"] = "eu" }))
        {
            Thread.Sleep(15);
        }

        listener.RecordObservableInstruments();

        // Assert
        durationRecords.Should().ContainSingle();
        var durationRecord = durationRecords.First();
        durationRecord.InstrumentName.Should().Be("Checkout.duration");
        durationRecord.Value.Should().BeGreaterThan(0.0);
        durationRecord.Tags["operation"].Should().Be("Checkout");
        durationRecord.Tags["status"].Should().Be("ok");
        durationRecord.Tags["region"].Should().Be("eu");

        totalRecords.Should().ContainSingle();
        var totalRecord = totalRecords.First();
        totalRecord.InstrumentName.Should().Be("Checkout.total");
        totalRecord.Value.Should().Be(1);
        totalRecord.Tags["operation"].Should().Be("Checkout");
        totalRecord.Tags["status"].Should().Be("ok");
        totalRecord.Tags["region"].Should().Be("eu");
    }

    [Fact]
    public void CardinalityProtection_OneThousandDistinctTags_LimitsDistinctCombinationsToLimitPlusOne()
    {
        // Acceptance Criterion 2: Generate 1,000 distinct tag values on a tracker with MaxUniqueValues = 50.
        // Assert that System.Diagnostics.Metrics records only at most 51 distinct tag combinations (the 50 distinct values + [Other]).

        var recordedTagValues = new ConcurrentBag<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.HighCardinalityService")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name.EndsWith(".total"))
            {
                var tagDict = ConvertTags(tags);
                if (tagDict.TryGetValue("user_id", out var userId))
                {
                    recordedTagValues.Add(userId);
                }
            }
        });

        listener.Start();

        var options = new MetricFlowOptions
        {
            Topic = "HighCardinalityService"
        };
        options.ConfigureMeters(m =>
        {
            m.MaxUniqueTagValues = 50;
            m.OverflowBucket = "[Other]";
        });

        var tracker = new MetricTracker(options);

        // Act: Generate 1,000 distinct tag values
        for (int i = 0; i < 1000; i++)
        {
            using (tracker.Track("ProcessUserAction", new() { ["user_id"] = $"user_{i}" }))
            {
                // no-op
            }
        }

        listener.RecordObservableInstruments();

        // Assert
        recordedTagValues.Count.Should().Be(1000);

        var distinctTagValues = recordedTagValues.Distinct().ToList();

        // Exactly 50 unique user IDs + 1 overflow bucket ("[Other]") = 51
        distinctTagValues.Count.Should().Be(51);
        distinctTagValues.Should().Contain("[Other]");

        // Count how many times [Other] was used: 1000 - 50 = 950 times
        recordedTagValues.Count(v => v == "[Other]").Should().Be(950);
    }

    [Fact]
    public void InFlightActiveCounter_IncrementsOnIn_AndDecrementsOnOut()
    {
        var activeEvents = new ConcurrentBag<(long Delta, Dictionary<string, string> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.ActiveTrackingTopic")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name.EndsWith(".active"))
            {
                activeEvents.Add((measurement, ConvertTags(tags)));
            }
        });

        listener.Start();

        var tracker = new MetricTracker("ActiveTrackingTopic");

        // Act
        using (tracker.Track("LongRunningJob", new() { ["tier"] = "premium" }))
        {
            activeEvents.Should().ContainSingle(e => e.Delta == 1);
            var inEvent = activeEvents.First(e => e.Delta == 1);
            inEvent.Tags["operation"].Should().Be("LongRunningJob");
            inEvent.Tags["tier"].Should().Be("premium");
        }

        // After dispose:
        activeEvents.Should().HaveCount(2);
        activeEvents.Should().Contain(e => e.Delta == -1);
        var outEvent = activeEvents.First(e => e.Delta == -1);
        outEvent.Tags["operation"].Should().Be("LongRunningJob");
        outEvent.Tags["tier"].Should().Be("premium");
    }

    [Fact]
    public void ItemsCounter_WhenBatchItemsTracked_PublishesItemsCount()
    {
        var itemEvents = new ConcurrentBag<(long Items, Dictionary<string, string> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.BatchTopic")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name.EndsWith(".items"))
            {
                itemEvents.Add((measurement, ConvertTags(tags)));
            }
        });

        listener.Start();

        var tracker = new MetricTracker("BatchTopic");

        // Act
        using (var scope = tracker.Track("BatchProcess"))
        {
            scope.SetItems(75);
        }

        // Assert
        itemEvents.Should().ContainSingle();
        var itemEvent = itemEvents.First();
        itemEvent.Items.Should().Be(75);
        itemEvent.Tags["operation"].Should().Be("BatchProcess");
    }

    [Fact]
    public void ExceptionTracking_WhenExceptionOccurs_PublishesExceptionCounterWithExceptionType()
    {
        var exceptionEvents = new ConcurrentBag<(long Count, Dictionary<string, string> Tags)>();
        var durationEvents = new ConcurrentBag<(double Duration, Dictionary<string, string> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.ExceptionTopic")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name.EndsWith(".exceptions"))
            {
                exceptionEvents.Add((measurement, ConvertTags(tags)));
            }
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name.EndsWith(".duration"))
            {
                durationEvents.Add((measurement, ConvertTags(tags)));
            }
        });

        listener.Start();

        var tracker = new MetricTracker("ExceptionTopic");

        // Act
        try
        {
            using (var scope = tracker.Track("FailingOperation"))
            {
                throw new InvalidOperationException("Something blew up");
            }
        }
        catch (InvalidOperationException ex)
        {
            // Tracker via extension or scope handles exception
            tracker.Out("FailingOperation", failed: true, exception: ex);
        }

        // Assert
        exceptionEvents.Should().NotBeEmpty();
        var exEvent = exceptionEvents.First();
        exEvent.Count.Should().Be(1);
        exEvent.Tags["operation"].Should().Be("FailingOperation");
        exEvent.Tags["exception.type"].Should().Be(typeof(InvalidOperationException).FullName);
        exEvent.Tags["status"].Should().Be("error");

        durationEvents.Should().NotBeEmpty();
        var durEvent = durationEvents.First(e => e.Tags["status"] == "error");
        durEvent.Tags["operation"].Should().Be("FailingOperation");
    }

    [Fact]
    public void DisabledMeters_WhenDisabled_DoesNotPublishAnyMeasurements()
    {
        var measurementsReceived = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name.Contains("DisabledTopic"))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((_, _, _, _) => Interlocked.Increment(ref measurementsReceived));
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => Interlocked.Increment(ref measurementsReceived));
        listener.Start();

        var options = new MetricFlowOptions { Topic = "DisabledTopic" };
        options.EnableMeters(false);

        var tracker = new MetricTracker(options);

        // Act
        using (tracker.Track("DisabledOp"))
        {
            // no-op
        }

        tracker.In("DirectIn");
        tracker.Out("DirectIn");

        // Assert
        measurementsReceived.Should().Be(0);
    }

    [Fact]
    public void MultiTopicIsolation_DifferentTopics_PublishUnderDistinctMeters()
    {
        var meterNames = new ConcurrentBag<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "DotnetKit.MetricFlow.OrdersTopic" ||
                instrument.Meter.Name == "DotnetKit.MetricFlow.BillingTopic")
            {
                meterNames.Add(instrument.Meter.Name);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();

        var tracker1 = new MetricTracker("OrdersTopic");
        var tracker2 = new MetricTracker("BillingTopic");

        using (tracker1.Track("PlaceOrder")) { }
        using (tracker2.Track("GenerateInvoice")) { }

        // Assert
        var distinctMeters = meterNames.Distinct().ToList();
        distinctMeters.Should().Contain("DotnetKit.MetricFlow.OrdersTopic");
        distinctMeters.Should().Contain("DotnetKit.MetricFlow.BillingTopic");
    }

    [Fact]
    public void DependencyInjection_AddMetricFlow_WiresUpMeterRegistryAndBridges()
    {
        var services = new ServiceCollection();

        services.AddMetricFlow(options =>
        {
            options.Topic = "RootService";
            options.ConfigureMeters(m =>
            {
                m.MaxUniqueTagValues = 100;
            });
        })
        .AddMetricTracker("WorkerService", opt =>
        {
            opt.ConfigureMeters(m =>
            {
                m.MaxUniqueTagValues = 20;
            });
        });

        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<MetricFlowRegistry>();
        var meterRegistry = provider.GetRequiredService<MetricFlowMeterRegistry>();

        var rootTracker = registry.GetTracker("RootService");
        var keyedWorker = provider.GetRequiredKeyedService<IMetricTracker>("WorkerService");

        rootTracker.MeterBridge.Should().NotBeNull();
        rootTracker.MeterBridge!.Meter.Name.Should().Be("DotnetKit.MetricFlow.RootService");

        keyedWorker.MeterBridge.Should().NotBeNull();
        keyedWorker.MeterBridge!.Meter.Name.Should().Be("DotnetKit.MetricFlow.WorkerService");
    }

    [Fact]
    public void SharedOperationNamingConvention_PublishesToSharedInstruments()
    {
        var instrumentNames = new ConcurrentBag<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name.Contains("SharedConventionTopic"))
            {
                instrumentNames.Add(instrument.Name);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();

        var options = new MetricFlowOptions { Topic = "SharedConventionTopic" };
        options.ConfigureMeters(m =>
        {
            m.NamingConvention = MetricInstrumentNamingConvention.SharedOperation;
        });

        var tracker = new MetricTracker(options);

        using (tracker.Track("ActionA")) { }
        using (tracker.Track("ActionB")) { }

        // Assert: instruments should be operation.duration, operation.total, etc.
        var distinctInstruments = instrumentNames.Distinct().ToList();
        distinctInstruments.Should().Contain("operation.duration");
        distinctInstruments.Should().Contain("operation.total");
        distinctInstruments.Should().NotContain("ActionA.duration");
        distinctInstruments.Should().NotContain("ActionB.duration");
    }

    [Fact]
    public void InAndOutDirectMethods_PublishAllMeasurements()
    {
        var totalRecorded = false;
        var durationRecorded = false;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name.Contains("DirectInOutTopic"))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name.EndsWith(".total")) totalRecorded = true;
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (instrument.Name.EndsWith(".duration")) durationRecorded = true;
        });

        listener.Start();

        var tracker = new MetricTracker("DirectInOutTopic");

        tracker.In("DirectOperation", new() { ["env"] = "staging" });
        Thread.Sleep(10);
        tracker.Out("DirectOperation", new() { ["env"] = "staging" });

        totalRecorded.Should().BeTrue();
        durationRecorded.Should().BeTrue();
    }

    [Fact]
    public void TagCardinalityGuard_DirectUnitTests_RespectsLimitsAndOverflowBucket()
    {
        var options = new MetricFlowMeterOptions
        {
            EnforceCardinalityLimitsOnMeters = true,
            MaxUniqueTagValues = 3,
            OverflowBucket = "[CustomOverflow]"
        };
        options.TagCardinalityLimits["special"] = 2;

        var guard = new TagCardinalityGuard(options);

        // General key with limit 3
        guard.SanitizeTagValue("city", "Paris").Should().Be("Paris");
        guard.SanitizeTagValue("city", "London").Should().Be("London");
        guard.SanitizeTagValue("city", "Berlin").Should().Be("Berlin");
        // Already seen values pass
        guard.SanitizeTagValue("city", "Paris").Should().Be("Paris");
        // 4th unique value should be clamped
        guard.SanitizeTagValue("city", "Tokyo").Should().Be("[CustomOverflow]");
        guard.SanitizeTagValue("city", "NewYork").Should().Be("[CustomOverflow]");

        // Special key with custom limit 2
        guard.SanitizeTagValue("special", "A").Should().Be("A");
        guard.SanitizeTagValue("special", "B").Should().Be("B");
        guard.SanitizeTagValue("special", "C").Should().Be("[CustomOverflow]");

        // Reset
        guard.Reset();
        guard.SanitizeTagValue("city", "Tokyo").Should().Be("Tokyo");
    }

    private static Dictionary<string, string> ConvertTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            result[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }
        return result;
    }
}
