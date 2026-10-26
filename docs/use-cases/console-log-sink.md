# Console Log Sink & Triggers

The **Console Log Sink & Triggers** pattern enables real-time formatted console logging of metric snapshots with ANSI color coding, latency and failure thresholds, and **timer-free execution-lifecycle sampling**. It eliminates the resource consumption of continuous background timers by evaluating sampling rules directly upon operation completion.

---

## Best For

- **Interactive CLI & Terminal Applications**: Rich, readable telemetry directly in developer terminals.
- **Batch Jobs & Migrations**: Instant visibility into batch progress, throughput, and error states.
- **Local Debugging & Performance Tuning**: Color-coded feedback on fast vs degraded execution paths without external telemetry collectors.
- **CI/CD Pipelines**: Clean execution summaries formatted as plain logs, tables, or markdown.

---

## Quick Start

Register [`ConsoleMetricSink`](xref:DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSink) directly on a standalone tracker or during DI setup, and configure lifecycle sampling triggers:

```csharp
using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Sinks.Console;

var tracker = new MetricTracker("OrderService")
    .AddThroughputCounter()
    .AddFailureCounter()
    // 1. Configure the Console Sink
    .AddConsoleSink(opt =>
    {
        opt.Colorize = true;
        opt.Prefix = "[OrderService:Sink]";
        opt.TimestampFormat = "HH:mm:ss.fff";
        opt.DurationUnit = DurationUnit.Auto;
    })
    // 2. Configure Timer-Free Lifecycle Sampling Triggers
    .ConfigureSinkSampling(sampling =>
    {
        // Stride trigger: Emit snapshot every 10 completed operations
        sampling.EmitEveryNExecutions = 10;

        // Tail / Outlier trigger: Emit immediately when an operation fails
        sampling.EmitOnFailure = true;

        // Latency trigger: Emit immediately if duration exceeds 100ms
        sampling.EmitOnSlowDurationThreshold = TimeSpan.FromMilliseconds(100);
    });

// 3. Execute operations
using (tracker.Track("ProcessOrder"))
{
    await Task.Delay(15);
}
```

---

## Available Options & API Reference

### 1. `ConsoleMetricSinkOptions`

[`ConsoleMetricSinkOptions`](xref:DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSinkOptions) controls the formatting, visual appearance, and threshold rules for console output:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Colorize` | `bool` | `true` | Enables ANSI terminal color formatting. |
| `IncludeTimestamp` | `bool` | `true` | Prepends timestamps to output lines. |
| `TimestampFormat` | `string` | `"HH:mm:ss.fff"` | Format string for emitted timestamps. |
| `IncludeTopicTags` | `bool` | `true` | Displays topic-level tags in the header output. |
| `Prefix` | `string` | `"[MetricFlow]"` | Label prepended to every output line. |
| `DisplayMode` | `ConsoleDisplayMode` | `FlatLog` | Output presentation style (`FlatLog`, `Table`, or `Markdown`). |
| `ShowHierarchicalTree` | `bool` | `true` | Renders nested parent-child trees (`▼`, `├─`, `└─`). |
| `ShowDimensionBreakdown` | `bool` | `true` | Renders dimension categories in table view. |
| `MaxDimensionValuesToRender` | `int` | `4` | Max unique dimension values shown before truncation. |
| `ShowTableHeaders` | `bool` | `true` | Renders table headers and divider rows. |
| `DurationUnit` | `DurationUnit` | `Milliseconds` | Duration formatting scale (`Milliseconds`, `Seconds`, `Auto`, etc.). |
| `DurationDecimals` | `int` | `2` | Number of decimal places for formatted durations. |
| `DurationFormatter` | `Func<TimeSpan, string>?` | `null` | Custom delegate overriding duration formatting. |
| `ThroughputUnit` | `string` | `"items/s"` | Label used for throughput processing rates. |
| `MemoryUnit` | `MemoryUnit` | `Auto` | Memory allocation unit (`Auto`, `Bytes`, `Kilobytes`, `Megabytes`, `Gigabytes`). |
| `MemoryDecimals` | `int` | `2` | Decimal places for memory formatting. |
| `MemoryFormatter` | `Func<long, string>?` | `null` | Custom delegate overriding byte formatting. |
| `OutputWriter` | `TextWriter?` | `Console.Out` | Target output stream (custom writer for testing/redirects). |
| `ColorSelector` | `Func<IMetricSnapshot, ConsoleColor?>?` | `null` | Dynamic delegate for pattern-matching snapshot colors. |
| `AddThreshold<TSnapshot>(...)` | Method | Fluent | Registers warn and critical duration or memory threshold rules. |

### 2. `MetricSinkTriggerOptions`

[`MetricSinkTriggerOptions`](xref:DotnetKit.MetricFlow.Sinks.MetricSinkTriggerOptions) configures timer-free execution triggers:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `EmitEveryNExecutions` | `int?` | `null` | Stride trigger: Emits snapshots every N completed operations (e.g., every 50 executions). |
| `EmitOnFailure` | `bool` | `false` | Tail trigger: Emits snapshots immediately if an operation fails or throws an exception. |
| `EmitOnSlowDurationThreshold` | `TimeSpan?` | `null` | Latency trigger: Emits snapshots immediately if duration exceeds this threshold. |
| `EmitSampleRate` | `double?` | `null` | Probabilistic sampling rate between `0.0` and `1.0` (e.g. 0.05 for 5% of operations). |

### 3. Manual Flush APIs

```csharp
// Synchronous flush of all registered sinks
tracker.FlushSinks();

// Asynchronous flush of all registered sinks
await tracker.FlushSinksAsync();
```

---

## Frequently Asked Questions (FAQ)

### Q: How do timer-free lifecycle triggers work, and why are they preferable to background timers?

**A:** Background timers constantly wake up CPU threads on fixed intervals regardless of system activity, causing idle CPU consumption and missing transient latency spikes. MetricFlow's lifecycle triggers evaluate your conditions (`EmitEveryNExecutions`, `EmitOnFailure`, `EmitOnSlowDurationThreshold`) inline as operations complete. This guarantees 100% capture of outliers with zero idle overhead.

---

### Q: How do I configure warning and critical color thresholds?

**A:** Use `opt.AddThreshold<DurationSnapshot>` or `opt.AddThreshold<MemorySnapshot>`:

```csharp
tracker.AddConsoleSink(opt =>
{
    opt.Colorize = true;

    // Green: < 50ms | DarkYellow: 50ms - 100ms | Red: >= 100ms
    opt.AddThreshold<DurationSnapshot>(
        warn: TimeSpan.FromMilliseconds(50),
        critical: TimeSpan.FromMilliseconds(100),
        warnColor: ConsoleColor.DarkYellow,
        criticalColor: ConsoleColor.Red,
        normalColor: ConsoleColor.Green
    );

    // Memory thresholds: Warn at 10 MB, Critical at 50 MB
    opt.AddThreshold<MemorySnapshot>(
        warn: 10 * 1024 * 1024,
        critical: 50 * 1024 * 1024
    );
});
```

---

### Q: How do I switch the console sink from FlatLog mode to Table mode?

**A:** Set `DisplayMode = ConsoleDisplayMode.Table` in the console sink options:

```csharp
tracker.AddConsoleSink(opt =>
{
    opt.DisplayMode = ConsoleDisplayMode.Table;
    opt.Colorize = true;
    opt.ShowTableHeaders = true;
    opt.ShowDimensionBreakdown = true;
});

// Emits an ASCII formatted table with aligned columns for Count, Duration, Throughput, and Memory
tracker.FlushSinks();
```

---

### Q: How can I redirect console sink output to a custom `TextWriter` for unit testing?

**A:** Assign a synchronized `StringWriter` or `StreamWriter` to `opt.OutputWriter`:

```csharp
using var writer = new StringWriter();

var tracker = new MetricTracker("TestTopic")
    .AddConsoleSink(opt =>
    {
        opt.OutputWriter = writer;
        opt.Colorize = false; // Plain text without ANSI escape codes
    });

using (tracker.Track("UnitTestOperation"))
{
    // execute test code
}

tracker.FlushSinks();
string capturedOutput = writer.ToString();
Assert.Contains("UnitTestOperation", capturedOutput);
```

---

### Q: How do I implement custom pattern matching across different metric snapshot types?

**A:** Assign the `ColorSelector` delegate with a C# switch expression:

```csharp
tracker.AddConsoleSink(opt =>
{
    opt.ColorSelector = snapshot => snapshot switch
    {
        ExceptionSnapshot { TotalExceptions: > 0 } => ConsoleColor.Red,
        FailureSnapshot { TotalFailures: > 0 } => ConsoleColor.Red,
        DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 100 => ConsoleColor.Red,
        DurationSnapshot d when d.AverageDuration.TotalMilliseconds > 50 => ConsoleColor.DarkYellow,
        MemorySnapshot m when m.AverageAllocatedBytes > 20 * 1024 * 1024 => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Green
    };
});
```
