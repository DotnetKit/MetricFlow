# Custom Counters Example

This example demonstrates how to author custom metric counters and register them into a specialized tracker using **MetricFlow**'s extensible architecture.

---

## Concepts

MetricFlow separates metric collection into independent counters inheriting from `CounterBase<TState>`. Each counter implements a state-token lifecycle:

- **`OnIn(in InContext context)`**: Invoked when an operation begins (`tracker.Track(...)` or `tracker.In(...)`). Returns a typed state token (`TState`) with **zero heap allocation**.
- **`OnOut(TState state, in OutContext context)`**: Invoked when the operation completes. Receives the original state token along with completion metadata (elapsed time, failure status, exception, tags).
- **`GetSnapshot(string metricName)` & `GetAllSnapshots()`**: Produces immutable snapshot records for reporting.
- **`Reset()`**: Clears or resets accumulated counter state.

---

## Code Walkthrough

### 1. Authoring a Custom Counter (`UtcDurationCounter.cs`)

Here, `UtcDurationCounter` measures duration using UTC clock ticks as its state token (`long`), wrapping MetricFlow's `DurationCounter`:

```csharp
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace CustomCounters;

/// <summary>
/// Counter based on UTC time using Pattern A state token.
/// </summary>
public class UtcDurationCounter(string name = "UtcDuration") : CounterBase<long>(name)
{
    private readonly DurationCounter _inner = new(name);

    public override long OnIn(in InContext context)
    {
        if (!IsEnabled) return 0;
        return DateTimeOffset.UtcNow.Ticks;
    }

    public override void OnOut(long state, in OutContext context)
    {
        if (!IsEnabled) return;
        TimeSpan elapsed = TimeSpan.Zero;
        if (state > 0)
        {
            elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - state);
        }
        _inner.OnOut(state, new OutContext(
            context.MetricName,
            context.Failed,
            context.Exception,
            elapsed,
            context.Tags,
            context.Metadata,
            context.UtcTimestamp));
    }

    public override IMetricSnapshot? GetSnapshot(string metricName) => _inner.GetSnapshot(metricName);
    public override IEnumerable<IMetricSnapshot> GetAllSnapshots() => _inner.GetAllSnapshots();
    public override void Reset() => _inner.Reset();
}
```

---

### 2. Creating a Specialized Tracker (`CustomMetricTrackerWithUtcCounter.cs`)

Subclass `MetricTrackerBase` and call `RegisterCounter(...)` in the constructor:

```csharp
using DotnetKit.MetricFlow.Abstractions;

namespace CustomCounters;

/// <summary>
/// Example of a custom metric tracker implementation with default UtcDurationCounter
/// </summary>
public class CustomMetricTrackerWithUtcCounter : MetricTrackerBase
{
    public CustomMetricTrackerWithUtcCounter(
        string topic,
        IReadOnlyDictionary<string, string>? topicTags = null,
        double? samplingRate = 1.0)
        : base(topic, topicTags, samplingRate)
    {
        RegisterCounter(new UtcDurationCounter());
    }
}
```

---

### 3. Usage

You can use your custom tracker just like the standard `MetricTracker`:

```csharp
var tracker = new CustomMetricTrackerWithUtcCounter("OrderTopic");

using (tracker.Track("ProcessOrder"))
{
    // Execution measured via UtcDurationCounter
    await Task.Delay(10);
}

Console.WriteLine(tracker.ToString());
```
