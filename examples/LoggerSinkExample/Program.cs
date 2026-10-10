using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace LoggerSinkExample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("================================================================");
        Console.WriteLine("    MetricFlow - Structured Logger Sink (Serilog) Example       ");
        Console.WriteLine("================================================================\n");

        // 1. Configure Serilog with rich console formatting
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            // 2. Build Dependency Injection container with Serilog and MetricFlow
            var services = new ServiceCollection();

            // Register Serilog as the Microsoft.Extensions.Logging provider
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // 3. Register MetricFlow with LoggerMetricSink and lifecycle sampling triggers
            services.AddMetricFlow("PaymentService", options =>
            {
                options.AddTagsEnricher(tags =>
                {
                    tags["environment"] = "Production";
                    tags["gateway"] = "Stripe";
                })
                .AddThroughputCounter()
                .AddFailureCounter();

                // Configure the ILogger Metric Sink
                options.AddLoggerSink(opt =>
                {
                    opt.CategoryName = "PaymentService.Telemetry";
                    opt.LogLevel = LogLevel.Information;       // Normal metrics logged as Info
                    opt.FailureLogLevel = LogLevel.Warning;    // Failures logged as Warning
                    opt.Prefix = "[PaymentService]";
                });

                // Configure timer-free lifecycle triggers (no background polling threads!)
                options.ConfigureSinkSampling(trig =>
                {
                    // Stride sampling: automatically log snapshots every 5 completed operations
                    trig.EmitEveryNExecutions = 5;

                    // Latency sampling: immediately log snapshots when an operation exceeds 100ms
                    trig.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100);

                    // Outlier / tail sampling: immediately log snapshots on operation failure
                    trig.EmitOnFailure = true;
                });
            });

            // Register application worker service
            services.AddTransient<PaymentProcessingService>();

            await using var provider = services.BuildServiceProvider();
            var paymentService = provider.GetRequiredService<PaymentProcessingService>();

            // Run simulated operational workflows
            await paymentService.ProcessPaymentsAsync();

            // 4. Standalone MetricTracker with LoggerMetricSink
            Console.WriteLine("\n>>> [Scenario 2] Standalone MetricTracker with Direct ILogger & Manual Flush <<<\n");

            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var standaloneLogger = loggerFactory.CreateLogger("PayoutService.Telemetry");

            var standaloneOptions = new MetricFlowOptions
            {
                Topic = "PayoutService"
            }
            .AddThroughputCounter()
            .AddFailureCounter();

            // Register logger sink passing the ILogger instance directly
            standaloneOptions.AddLoggerSink(standaloneLogger, opt =>
            {
                opt.LogLevel = LogLevel.Information;
                opt.Prefix = "[PayoutService]";
            });

            using var payoutTracker = new MetricTracker(standaloneOptions);

            Console.WriteLine("Tracking payout operations in standalone tracker...");
            for (int i = 1; i <= 3; i++)
            {
                using (payoutTracker.TrackItems("ExecutePayout", itemCount: i * 10))
                {
                    await Task.Delay(15);
                }
            }

            Console.WriteLine("Manually flushing sinks at shutdown via FlushSinks()...\n");
            payoutTracker.FlushSinks();

            Console.WriteLine("\nLogger sink (Serilog) example completed successfully!");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}

internal class PaymentProcessingService
{
    private readonly IMetricTracker _tracker;

    public PaymentProcessingService(IMetricTracker tracker)
    {
        _tracker = tracker;
    }

    public async Task ProcessPaymentsAsync()
    {
        Console.WriteLine("Step 1: Processing 10 successful payments (triggers every 5 executions via EmitEveryNExecutions)...");
        for (int i = 1; i <= 10; i++)
        {
            using (_tracker.TrackItems("AuthorizePayment", itemCount: i * 5, additionalTags: new() { ["card_type"] = "Visa" }))
            {
                await Task.Delay(10);
            }
        }

        Console.WriteLine("\nStep 2: Processing slow payment (triggers immediately via EmitOnSlowDurationThreshold > 100ms)...");
        using (_tracker.Track("AuthorizePayment", tags: new() { ["card_type"] = "Mastercard", ["route"] = "3DSecure" }))
        {
            await Task.Delay(120); // Exceeds the 100ms latency threshold
        }

        Console.WriteLine("\nStep 3: Processing payment with logical decline (triggers immediately at Warning level via EmitOnFailure)...");
        using (var scope = _tracker.Track("AuthorizePayment", tags: new() { ["card_type"] = "Amex", ["reason"] = "InsufficientFunds" }))
        {
            await Task.Delay(10);
            scope.SetFailed(true); // Flag business/logical failure
        }

        Console.WriteLine("\nStep 4: Processing payment encountering a network exception (triggers immediately via EmitOnFailure)...");
        try
        {
            await _tracker.TrackActionAsync("AuthorizePayment", async () =>
            {
                await Task.Delay(10);
                throw new TimeoutException("Gateway timeout communicating with acquiring bank");
            }, tags: new() { ["card_type"] = "Visa", ["channel"] = "ECommerce" });
        }
        catch (TimeoutException)
        {
            // Expected simulation
        }
    }
}
