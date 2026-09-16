using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

public class MemoryCounter : ICounter
{
    public const string DefaultCounterName = "Memory";

    private readonly ConcurrentDictionary<string, MetricMemoryState> _states = new();

    public string Name { get; }
    public bool IsEnabled { get; set; } = true;

    public MemoryCounter(string name = DefaultCounterName)
    {
        Name = name;
    }

    public object? OnIn(in InContext context)
    {
        if (!IsEnabled) return null;

        return new MemoryTrackingToken(
            Environment.CurrentManagedThreadId,
            GC.GetAllocatedBytesForCurrentThread(),
            GC.GetTotalAllocatedBytes(precise: false));
    }

    public void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        long allocatedBytes = 0;
        if (state is MemoryTrackingToken token)
        {
            if (Environment.CurrentManagedThreadId == token.ThreadId)
            {
                var current = GC.GetAllocatedBytesForCurrentThread();
                allocatedBytes = current >= token.ThreadBytes ? current - token.ThreadBytes : 0;
            }
            else
            {
                var currentTotal = GC.GetTotalAllocatedBytes(precise: false);
                allocatedBytes = currentTotal >= token.TotalBytes ? currentTotal - token.TotalBytes : 0;
            }
        }

        var metricState = _states.GetOrAdd(context.MetricName, static name => new MetricMemoryState(name));
        metricState.Record(allocatedBytes);
    }

    private readonly record struct MemoryTrackingToken(int ThreadId, long ThreadBytes, long TotalBytes);

    public IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _states.TryGetValue(metricName, out var state) ? state.ToSnapshot(Name) : null;
    }

    public IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var state in _states.Values)
        {
            yield return state.ToSnapshot(Name);
        }
    }

    public void Reset() => _states.Clear();

    public MetricMemoryState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    public class MetricMemoryState(string metricName)
    {
        private long _operationCount;
        private long _totalAllocatedBytes;
        private long _minAllocatedBytes;
        private long _maxAllocatedBytes;

        public string MetricName => metricName;
        public long OperationCount => Interlocked.Read(ref _operationCount);
        public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);
        public long MinAllocatedBytes => Interlocked.Read(ref _minAllocatedBytes);
        public long MaxAllocatedBytes => Interlocked.Read(ref _maxAllocatedBytes);
        public long AverageAllocatedBytes
        {
            get
            {
                var count = OperationCount;
                return count > 0 ? TotalAllocatedBytes / count : 0;
            }
        }

        public void Record(long allocatedBytes)
        {
            Interlocked.Increment(ref _operationCount);
            Interlocked.Add(ref _totalAllocatedBytes, allocatedBytes);

            UpdateMax(allocatedBytes);
            UpdateMin(allocatedBytes);
        }

        private void UpdateMax(long bytes)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref _maxAllocatedBytes);
                if (bytes <= current) break;
            } while (Interlocked.CompareExchange(ref _maxAllocatedBytes, bytes, current) != current);
        }

        private void UpdateMin(long bytes)
        {
            long current;
            do
            {
                current = Interlocked.Read(ref _minAllocatedBytes);
                if (current != 0 && bytes >= current) break;
            } while (Interlocked.CompareExchange(ref _minAllocatedBytes, bytes, current) != current);
        }

        public MemorySnapshot ToSnapshot(string counterName)
        {
            return new MemorySnapshot(
                MetricName: metricName,
                CounterName: counterName,
                OperationCount: OperationCount,
                TotalAllocatedBytes: TotalAllocatedBytes,
                AverageAllocatedBytes: AverageAllocatedBytes,
                MinAllocatedBytes: MinAllocatedBytes,
                MaxAllocatedBytes: MaxAllocatedBytes,
                Timestamp: DateTime.UtcNow
            );
        }
    }
}

public record MemorySnapshot(
    string MetricName,
    string CounterName,
    long OperationCount,
    long TotalAllocatedBytes,
    long AverageAllocatedBytes,
    long MinAllocatedBytes,
    long MaxAllocatedBytes,
    DateTime Timestamp) : IMetricSnapshot
{

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Operations: {OperationCount}");
        sb.AppendLine($"Avg allocated: {FormatBytes(AverageAllocatedBytes)}");
        sb.AppendLine($"Allocated (min, max): {FormatBytes(MinAllocatedBytes)} / {FormatBytes(MaxAllocatedBytes)}");
        sb.AppendLine($"Total allocated: {FormatBytes(TotalAllocatedBytes)}");
        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    public override string ToString() => ToFormattedString();
}
