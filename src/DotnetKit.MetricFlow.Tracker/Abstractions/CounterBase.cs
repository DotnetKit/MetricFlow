using System.Text;
using DotnetKit.MetricFlow.Tracker.Abstractions;
using DotnetKit.MetricFlow.Tracker.Extensions;

public abstract class CounterBase(string name, Dictionary<string, string>? metricMetadata) : ICounter
{
    public DateTime TimeStamp => DateTime.UtcNow;
    private DateTime? _startedAt = null;
    private DateTime? _endedAt = null;
    private long _inCount = 0;
    private long _failedCount = 0;
    private long _outCount = 0;
    private long _totalDuration = 0;

    private long _maxDuration = 0;
    private long _minDuration = 0;
    private long _averageDuration = 0;

    public string Name => name;
    public Dictionary<string, string> Metadata => metricMetadata ?? [];

    public CounterValues Values => new CounterValues(
        Interlocked.Read(ref _inCount),
        Interlocked.Read(ref _outCount),
        Interlocked.Read(ref _failedCount),
        TimeSpan.FromMilliseconds(Interlocked.Read(ref _totalDuration)),
        TimeSpan.FromMilliseconds(Interlocked.Read(ref _averageDuration)),
        TimeSpan.FromMilliseconds(Interlocked.Read(ref _minDuration)),
        TimeSpan.FromMilliseconds(Interlocked.Read(ref _maxDuration))

        );

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

    protected void FinalizeState(long duration)
    {
        _endedAt = DateTime.UtcNow;
        Interlocked.Add(ref _totalDuration, duration);

        UpdateMaxDuration(duration);
        UpdateMinDuration(duration);
        UpdateAverageDuration(duration);
    }

    private void UpdateMaxDuration(long duration)
    {
        long initialValue, computedValue;
        do
        {
            initialValue = _maxDuration;
            if (duration <= initialValue) break;
            computedValue = duration;
        } while (Interlocked.CompareExchange(ref _maxDuration, computedValue, initialValue) != initialValue);
    }

    private void UpdateMinDuration(long duration)
    {
        long initialValue, computedValue;
        do
        {
            initialValue = _minDuration;
            if (initialValue != 0 && duration >= initialValue) break;
            computedValue = duration;
        } while (Interlocked.CompareExchange(ref _minDuration, computedValue, initialValue) != initialValue);
    }

    private void UpdateAverageDuration(long duration)
    {
        long initialValue, computedValue;
        do
        {
            initialValue = _averageDuration;
            computedValue = initialValue == 0 ? duration : (initialValue + duration) / 2;
        } while (Interlocked.CompareExchange(ref _averageDuration, computedValue, initialValue) != initialValue);
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