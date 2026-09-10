using System.Text;
using DotnetKit.MetricFlow.Tracker.Extensions;

namespace DotnetKit.MetricFlow.Tracker.Abstractions;

public abstract class CounterBase(string name, Dictionary<string, string>? metricMetadata) : ICounter
{
    public DateTime TimeStamp => DateTime.UtcNow;
    private DateTime? _startedAt;
    private DateTime? _endedAt;
    private long _inCount;
    private long _failedCount;
    private long _outCount;
    private long _totalDurationTicks;
    private long _maxDurationTicks;
    private long _minDurationTicks;

    public string Name => name;
    public Dictionary<string, string> Metadata => metricMetadata ?? [];

    public CounterValues Values
    {
        get
        {
            var inCount = Interlocked.Read(ref _inCount);
            var outCount = Interlocked.Read(ref _outCount);
            var failedCount = Interlocked.Read(ref _failedCount);
            var totalTicks = Interlocked.Read(ref _totalDurationTicks);
            var minTicks = Interlocked.Read(ref _minDurationTicks);
            var maxTicks = Interlocked.Read(ref _maxDurationTicks);
            var avgTicks = outCount > 0 ? totalTicks / outCount : 0;

            return new CounterValues(
                inCount,
                outCount,
                failedCount,
                TimeSpan.FromTicks(totalTicks),
                TimeSpan.FromTicks(avgTicks),
                TimeSpan.FromTicks(minTicks),
                TimeSpan.FromTicks(maxTicks)
            );
        }
    }

    public abstract void Start();

    public abstract long Stop();

    public long Inc()
    {
        Start();
        _startedAt = DateTime.UtcNow;
        return Interlocked.Increment(ref _inCount);
    }

    public long Dec(bool? failed = false)
    {
        FinalizeState(Stop());

        if (failed == true)
        {
            Interlocked.Increment(ref _failedCount);
        }
        return Interlocked.Increment(ref _outCount);
    }

    public long Dec(TimeSpan duration, bool? failed = false)
    {
        FinalizeState(duration.Ticks);

        if (failed == true)
        {
            Interlocked.Increment(ref _failedCount);
        }
        return Interlocked.Increment(ref _outCount);
    }

    protected void FinalizeState(long durationTicks)
    {
        if (durationTicks < 0)
        {
            durationTicks = 0;
        }

        _endedAt = DateTime.UtcNow;
        Interlocked.Add(ref _totalDurationTicks, durationTicks);

        UpdateMaxDuration(durationTicks);
        UpdateMinDuration(durationTicks);
    }

    private void UpdateMaxDuration(long durationTicks)
    {
        long initialValue, computedValue;
        do
        {
            initialValue = _maxDurationTicks;
            if (durationTicks <= initialValue) break;
            computedValue = durationTicks;
        } while (Interlocked.CompareExchange(ref _maxDurationTicks, computedValue, initialValue) != initialValue);
    }

    private void UpdateMinDuration(long durationTicks)
    {
        long initialValue, computedValue;
        do
        {
            initialValue = _minDurationTicks;
            if (initialValue != 0 && durationTicks >= initialValue) break;
            computedValue = durationTicks;
        } while (Interlocked.CompareExchange(ref _minDurationTicks, computedValue, initialValue) != initialValue);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Name);
        sb.AppendLine(Metadata.ToFormattedString("MetricMetadata"));
        sb.AppendLine(Values.ToString());

        return sb.ToString();
    }
}