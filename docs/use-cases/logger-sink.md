# Logger Sink (Serilog & Microsoft.Extensions.Logging)

The **Logger Sink** pattern routes MetricFlow snapshots into the standard .NET logging abstraction ([`ILogger`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Sinks/Logger/LoggerMetricSink.cs)). It supports modern structured logging frameworks such as **Serilog**, **NLog**, **Log4Net**, and cloud log forwarders (Datadog Logs, ELK Stack, AWS CloudWatch, Azure Application Insights).

---

## Best For

- **Centralized Structured Logging**: Ingest metric events and performance summaries alongside application business logs.
- **APM & Cloud Forwarders**: Forward operational snapshots into existing enterprise logging pipelines without extra daemons.
- **Automated Alerting**: Escalate failed or degraded metric snapshots to `Warning` or `Error` log levels to trigger pager alerts.
- **Audit Trails**: Retain persistent logs of execution counts, throughput rates, and memory allocations.

---

## Quick Start

Register [`LoggerMetricSink`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Sinks/Logger/LoggerMetricSink.cs) using an `ILoggerFactory`, an existing `ILogger`, or via standard dependency injection:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.Logging;

// 1. Create your ILoggerFactory (or resolve from DI)
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// 2. Initialize tracker and attach Logger Sink
var tracker = new MetricTracker("PaymentService", new()
{
    ["environment"] = "Production",
    ["gateway"] = "Stripe"
})
.AddThroughputCounter()
.AddFailureCounter()
.AddLoggerSink(loggerFactory, opt =>
{
    opt.CategoryName = "PaymentService.Telemetry";
    opt.LogLevel = LogLevel.Information;    // Normal metrics logged as Info
    opt.FailureLogLevel = LogLevel.Warning; // Failures logged as Warning
    opt.Prefix = "[PaymentService]";
})
.ConfigureSinkSampling(trig =>
{
    // Stride trigger: Log summary every 10 operations
    trig.EmitEveryNExecutions = 10;

    // Tail trigger: Immediately log on failure
    trig.EmitOnFailure = true;

    // Latency trigger: Immediately log if duration > 100ms
    trig.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100);
});

// 3. Execute operations
using (tracker.Track("AuthorizePayment"))
{
    await Task.Delay(15);
}
```

---

## Available Options & API Reference

### 1. `LoggerMetricSinkOptions`

[`LoggerMetricSinkOptions`](file:///Users/evomac1/gh/DotnetKit/MetricFlow/src/DotnetKit.MetricFlow/Sinks/Logger/LoggerMetricSinkOptions.cs) configures log categories, severity levels, event IDs, and message prefixes:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Logger` | `ILogger?` | `null` | Pre-existing logger instance. If null, created via `ILoggerFactory`. |
| `CategoryName` | `string` | `"DotnetKit.MetricFlow"` | Logger category name when creating from `ILoggerFactory`. |
| `LogLevel` | `LogLevel` | `LogLevel.Information` | Default log level for healthy metric snapshots. |
| `FailureLogLevel` | `LogLevel` | `LogLevel.Warning` | Log level used when a snapshot contains failures or exceptions. |
| `LogLevelSelector` | `Func<IMetricSnapshot, LogLevel>?` | `null` | Custom delegate to dynamically compute log level per snapshot. |
| `EventId` | `EventId` | `1001, "MetricSnapshot"` | Structured `EventId` attached to log events. |
| `IncludeTimestamp` | `bool` | `false` | Prepends timestamps (defaults to false as logging providers already add timestamps). |
| `TimestampFormat` | `string` | `"HH:mm:ss.fff"` | Timestamp format string if `IncludeTimestamp` is enabled. |
| `Prefix` | `string?` | `null` | Optional text prefix prepended to emitted log messages. |

### 2. Registration Overloads

```csharp
// Standalone tracker with ILoggerFactory
tracker.AddLoggerSink(loggerFactory, opt => { ... });

// Standalone tracker with direct ILogger instance
tracker.AddLoggerSink(loggerInstance, opt => { ... });

// DI-based registration (automatically resolves ILoggerFactory from DI)
services.AddMetricFlow("BillingService", options =>
{
    options.AddLoggerSink(opt =>
    {
        opt.CategoryName = "MyApp.Metrics";
    });
});
```

---

## Frequently Asked Questions (FAQ)

### Q: How do I route normal operations to `Information` and failures to `Warning` or `Error`?

**A:** By default, `LogLevel = LogLevel.Information` and `FailureLogLevel = LogLevel.Warning`. If you wish to escalate failures to `Error`:

```csharp
options.AddLoggerSink(opt =>
{
    opt.LogLevel = LogLevel.Information;
    opt.FailureLogLevel = LogLevel.Error; // Escalate failures to Error
});
```

---

### Q: How do I integrate MetricFlow with Serilog?

**A:** Serilog implements standard Microsoft `ILoggerFactory`. Configure Serilog globally, then MetricFlow will automatically output structured log events:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/metrics-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// DI registration uses the Serilog-backed ILoggerFactory
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
builder.Services.AddMetricFlow("OrderService", options =>
{
    options.AddLoggerSink(opt => opt.CategoryName = "OrderService.Metrics");
});
```

---

### Q: How do I dynamically set `LogLevel` based on operation latency?

**A:** Assign the `LogLevelSelector` delegate in `LoggerMetricSinkOptions`:

```csharp
options.AddLoggerSink(opt =>
{
    opt.LogLevelSelector = snapshot =>
    {
        if (snapshot is DurationSnapshot d)
        {
            if (d.AverageDuration.TotalMilliseconds > 500) return LogLevel.Error;
            if (d.AverageDuration.TotalMilliseconds > 150) return LogLevel.Warning;
        }

        if (snapshot is FailureSnapshot f && f.TotalFailures > 0)
        {
            return LogLevel.Warning;
        }

        return LogLevel.Information;
    };
});
```

---

### Q: How can I filter or silence MetricFlow logs in `appsettings.json`?

**A:** Configure the logging category specified in `opt.CategoryName` (defaults to `"DotnetKit.MetricFlow"`):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DotnetKit.MetricFlow": "Warning"
    }
  }
}
```

---

### Q: When should `FlushSinks()` or `FlushSinksAsync()` be called with `ILogger`?

**A:** MetricFlow emits logs automatically whenever a lifecycle trigger matches (`EmitEveryNExecutions`, `EmitOnFailure`, `EmitOnSlowDurationThreshold`). You should explicitly call `tracker.FlushSinks()` or `await tracker.FlushSinksAsync()` during graceful shutdown (e.g. before exiting `Main` or in `IHostedService.StopAsync`) to ensure all in-memory buffers are flushed to your logger.
