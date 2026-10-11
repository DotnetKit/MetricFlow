using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks.Console;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleSinkExample;

internal class Program
{
    private static async Task Main()
    {
        Console.WriteLine("================================================================");
        Console.WriteLine("        MetricFlow - Structured Console Log Sink Example        ");
        Console.WriteLine("================================================================\n");

        // ---------------------------------------------------------------------
        // Scenario 1: Dependency Injection & Timer-Free Lifecycle Triggers
        // ---------------------------------------------------------------------
        Console.WriteLine(">>> [Scenario 1] Dependency Injection & Timer-Free Lifecycle Triggers <<<\n");

        var services = new ServiceCollection();

        // Register MetricFlow with the structured console sink and execution triggers
        services.AddMetricFlow("OrderService", options =>
        {
            // Configure topic tags and counters
            options.AddTagsEnricher(tags =>
            {
                tags["environment"] = "Production";
                tags["region"] = "us-east-1";
            })
            .AddThroughputCounter()
            .AddFailureCounter();

            // 1. Register ConsoleMetricSink with custom formatting options
            options.AddConsoleSink(opt =>
            {
                opt.Colorize = true;                 // Enable ANSI color coding
                opt.IncludeTimestamp = true;         // Include timestamp prefix
                opt.TimestampFormat = "HH:mm:ss.fff";// Millisecond precision
                opt.Prefix = "[OrderService:Sink]";  // Log prefix tag
            });

            // 2. Configure execution-lifecycle sampling triggers (zero background timers!)
            options.ConfigureSinkSampling(sampling =>
            {
                // Stride trigger: Emit snapshots every 5 completed operations
                sampling.EmitEveryNExecutions = 5;

                // Tail / Outlier trigger: Emit snapshots immediately when an operation fails
                sampling.EmitOnFailure = true;

                // Latency trigger: Emit snapshots immediately if duration exceeds 100ms
                sampling.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100);
            });
        });

        // Register application worker service
        services.AddTransient<OrderFulfillmentService>();

        await using var provider = services.BuildServiceProvider();
        var orderService = provider.GetRequiredService<OrderFulfillmentService>();

        // Execute orders to demonstrate triggers firing in real-time
        await orderService.ProcessOrdersAsync();

        // ---------------------------------------------------------------------
        // Scenario 2: Standalone MetricTracker with ConsoleMetricSink & Manual Flush
        // ---------------------------------------------------------------------
        Console.WriteLine("\n>>> [Scenario 2] Standalone MetricTracker & Manual Sink Flush <<<\n");

        var standaloneOptions = new MetricFlowOptions
        {
            Topic = "WarehouseService"
        }
        .AddThroughputCounter()
        .AddFailureCounter();

        // Configure console sink with custom options
        standaloneOptions.AddConsoleSink(opt =>
        {
            opt.Colorize = true;
            opt.Prefix = "[Warehouse:Sink]";
        });

        using var warehouseTracker = new MetricTracker(standaloneOptions);

        Console.WriteLine("Tracking operations in standalone tracker (no automatic triggers configured)...");
        for (var i = 1; i <= 3; i++)
        {
            using (warehouseTracker.TrackItems("InventoryRestock", itemCount: i * 25))
            {
                await Task.Delay(15);
            }
        }

        // ---------------------------------------------------------------------
        // Scenario 3: Threshold-Based Color Coding & Custom Color Selectors
        // ---------------------------------------------------------------------
        Console.WriteLine("\n>>> [Scenario 3] Native Threshold Color Coding & Dynamic Color Selector <<<\n");

        var paymentOptions = new MetricFlowOptions
        {
            Topic = "PaymentGateway"
        };

        // Configure console sink with native duration thresholds and custom color rules
        paymentOptions.AddConsoleSink(opt =>
        {
            opt.Colorize = true;
            opt.Prefix = "[Payment:Thresholds]";

            // Native threshold rule:
            // - Average duration < 50ms: Green
            // - Average duration 50ms - 100ms: DarkYellow / Warning
            // - Average duration >= 100ms: Red / Critical
            opt.AddThreshold<DurationSnapshot>(
                warn: TimeSpan.FromMilliseconds(50),
                critical: TimeSpan.FromMilliseconds(100)
            );
        });

        using var paymentTracker = new MetricTracker(paymentOptions);

        Console.WriteLine("1. Fast payment operation (< 50ms -> Green)...");
        using (paymentTracker.Track("AuthorizePayment_Normal"))
        {
            await Task.Delay(20);
        }
        await paymentTracker.FlushSinksAsync();

        Console.WriteLine("\n2. Degraded payment operation (50ms - 100ms -> DarkYellow Warning)...");
        using (paymentTracker.Track("AuthorizePayment_Degraded"))
        {
            await Task.Delay(65);
        }
        await paymentTracker.FlushSinksAsync();

        Console.WriteLine("\n3. Critical latency payment operation (> 100ms -> Red Critical)...");
        using (paymentTracker.Track("AuthorizePayment_Critical"))
        {
            await Task.Delay(125);
        }
        await paymentTracker.FlushSinksAsync();

        // ---------------------------------------------------------------------
        // Scenario 4: Hierarchical Execution Trees & Multi-Counter Custom Colors
        // ---------------------------------------------------------------------
        Console.WriteLine("\n>>> [Scenario 4] Hierarchical Parent-Child Scopes & Custom Colors <<<\n");

        var hierarchyOptions = new MetricFlowOptions
        {
            Topic = "MediaCatalog"
        }
        .AddHierarchyCounter()
        .AddMemoryCounter()
        .AddThroughputCounter();

        hierarchyOptions.AddConsoleSink(opt =>
        {
            opt.Colorize = true;
            opt.Prefix = "[Catalog:Trace]";
            opt.ShowHierarchicalTree = true;

            // 1. Duration thresholds
            opt.AddThreshold<DurationSnapshot>(
                warn: TimeSpan.FromMilliseconds(50),
                critical: TimeSpan.FromMilliseconds(100),
                warnColor: ConsoleColor.DarkYellow,
                criticalColor: ConsoleColor.Red,
                normalColor: ConsoleColor.Green);

            // 2. Memory allocation thresholds
            opt.AddThreshold<MemorySnapshot>(
                warn: 10 * 1024 * 1024,      // 10 MB
                critical: 50 * 1024 * 1024,  // 50 MB
                warnColor: ConsoleColor.DarkYellow,
                criticalColor: ConsoleColor.Red);

            // 3. Or use a dynamic pattern-matching selector for total control
            opt.ColorSelector = snapshot => snapshot switch
            {
                ExceptionSnapshot { TotalExceptions: > 0 } => ConsoleColor.Red,
                FailureSnapshot { TotalFailures: > 0 } => ConsoleColor.Red,
                DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 100 => ConsoleColor.Red,
                DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 50 => ConsoleColor.DarkYellow,
                MemorySnapshot m when m.AverageAllocatedBytes > 50 * 1024 * 1024 => ConsoleColor.Red,
                MemorySnapshot m when m.AverageAllocatedBytes > 10 * 1024 * 1024 => ConsoleColor.DarkYellow,
                _ => ConsoleColor.Green
            };

            // 4. Unit customization (Duration, Memory, Throughput)
            opt.DurationUnit = DurationUnit.Auto; // Automatically scales between ms, s, m, h
            opt.MemoryUnit = MemoryUnit.Auto;     // Automatically scales between B, KB, MB, GB
            opt.ThroughputUnit = "epg_items/s";
        });

        using var catalogTracker = new MetricTracker(hierarchyOptions);

        Console.WriteLine("Executing parent operation with nested child operations...");
        using (catalogTracker.Track("GetChannelProgramsUseCase"))
        {
            await Task.Delay(30);

            // Child 1: Quick lookup
            using (var step1 = catalogTracker.Track("MatchSingleChannelUseCase"))
            {
                step1.SetItems(1);
                await Task.Delay(10);
            }

            // Child 2: Data fetch (triggers warning threshold > 50ms)
            using (var step2 = catalogTracker.Track("TableStorage.GetProgrammesForChannel"))
            {
                step2.SetItems(1450);
                await Task.Delay(65);

                // Grandchild operations
                using (catalogTracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(25);
                }

                using (catalogTracker.Track("TableStorage.QuerySegmentAsync"))
                {
                    await Task.Delay(25);
                }
            }
        }

        // Flush and display the execution tree
        await catalogTracker.FlushSinksAsync();

        Console.WriteLine("\nStructured console log sink example completed successfully!");
    }
}

internal class OrderFulfillmentService(IMetricTracker tracker)
{
    public async Task ProcessOrdersAsync()
    {
        Console.WriteLine("Step 1: Processing 10 regular orders (triggers every 5 executions via EmitEveryNExecutions)...");
        for (var i = 1; i <= 10; i++)
        {
            using (tracker.TrackItems("ProcessOrder", itemCount: i * 10, additionalTags: new() { ["tier"] = "standard" }))
            {
                await Task.Delay(10);
            }
        }

        Console.WriteLine("\nStep 2: Processing slow order (triggers immediately via EmitOnSlowDurationThreshold > 100ms)...");
        using (tracker.Track("ProcessOrder", tags: new() { ["tier"] = "slow_backend" }))
        {
            await Task.Delay(125); // Exceeds the 100ms threshold
        }

        Console.WriteLine("\nStep 3: Processing order with logical failure (triggers immediately via EmitOnFailure)...");
        using (var scope = tracker.Track("ProcessOrder", tags: new() { ["tier"] = "payment_declined" }))
        {
            await Task.Delay(10);
            scope.SetFailed(); // Flag logical failure without exception
        }

        Console.WriteLine("\nStep 4: Processing order that throws an exception (triggers immediately via EmitOnFailure)...");
        try
        {
            await tracker.TrackActionAsync("ProcessOrder", async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("External payment gateway connection timeout");
            }, tags: new() { ["tier"] = "gateway_down" });
        }
        catch (InvalidOperationException)
        {
            // Handled expected simulation exception
        }
    }
}
