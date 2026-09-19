using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow.Extensions;

/// <summary>
/// Provides extension methods for computing deltas between snapshots and aggregating snapshot collections.
/// </summary>
public static class SnapshotDeltaExtensions
{
    /// <summary>
    /// Computes the delta (change) between the current snapshot and an optional previous snapshot.
    /// If previous is null, returns the current snapshot as the initial delta.
    /// </summary>
    public static IMetricSnapshot ComputeDelta(this IMetricSnapshot current, IMetricSnapshot? previous)
    {
        if (previous == null)
        {
            return current;
        }

        if (current is DurationSnapshot curDur && previous is DurationSnapshot prevDur)
        {
            var inDelta = Math.Max(0, curDur.InCount - prevDur.InCount);
            var outDelta = Math.Max(0, curDur.OutCount - prevDur.OutCount);
            var failedDelta = Math.Max(0, curDur.FailedCount - prevDur.FailedCount);

            // Handle potential counter reset
            var totalDurationDelta = curDur.TotalDuration >= prevDur.TotalDuration
                ? curDur.TotalDuration - prevDur.TotalDuration
                : curDur.TotalDuration;

            var avgDelta = outDelta > 0
                ? TimeSpan.FromTicks(totalDurationDelta.Ticks / outDelta)
                : TimeSpan.Zero;

            var minDelta = outDelta > 0 ? curDur.MinDuration : TimeSpan.Zero;
            var maxDelta = outDelta > 0 ? curDur.MaxDuration : TimeSpan.Zero;

            return new DurationSnapshot(
                MetricName: curDur.MetricName,
                CounterName: curDur.CounterName,
                InCount: inDelta,
                OutCount: outDelta,
                FailedCount: failedDelta,
                TotalDuration: totalDurationDelta,
                AverageDuration: avgDelta,
                MinDuration: minDelta,
                MaxDuration: maxDelta,
                Timestamp: curDur.Timestamp
            );
        }

        if (current is MemorySnapshot curMem && previous is MemorySnapshot prevMem)
        {
            var opDelta = Math.Max(0, curMem.OperationCount - prevMem.OperationCount);
            var totalBytesDelta = curMem.TotalAllocatedBytes >= prevMem.TotalAllocatedBytes
                ? curMem.TotalAllocatedBytes - prevMem.TotalAllocatedBytes
                : curMem.TotalAllocatedBytes;

            var avgBytes = opDelta > 0 ? totalBytesDelta / opDelta : 0;
            var minBytes = opDelta > 0 ? curMem.MinAllocatedBytes : 0;
            var maxBytes = opDelta > 0 ? curMem.MaxAllocatedBytes : 0;

            return new MemorySnapshot(
                MetricName: curMem.MetricName,
                CounterName: curMem.CounterName,
                OperationCount: opDelta,
                TotalAllocatedBytes: totalBytesDelta,
                AverageAllocatedBytes: avgBytes,
                MinAllocatedBytes: minBytes,
                MaxAllocatedBytes: maxBytes,
                Timestamp: curMem.Timestamp
            );
        }

        if (current is ExceptionSnapshot curEx && previous is ExceptionSnapshot prevEx)
        {
            var opsDelta = Math.Max(0, curEx.TotalOperations - prevEx.TotalOperations);
            var failDelta = Math.Max(0, curEx.TotalFailures - prevEx.TotalFailures);

            var breakdownDelta = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var (exType, count) in curEx.ExceptionsByType)
            {
                var prevCount = prevEx.ExceptionsByType.TryGetValue(exType, out var pc) ? pc : 0;
                var diff = Math.Max(0, count - prevCount);
                if (diff > 0)
                {
                    breakdownDelta[exType] = diff;
                }
            }

            return new ExceptionSnapshot(
                MetricName: curEx.MetricName,
                CounterName: curEx.CounterName,
                TotalOperations: opsDelta,
                TotalFailures: failDelta,
                ExceptionsByType: breakdownDelta,
                Timestamp: curEx.Timestamp
            );
        }

        return current;
    }

    /// <summary>
    /// Aggregates a sequence of delta snapshots (grouped by metric and counter) into consolidated snapshots.
    /// Useful for time-range rollups [start, end] and multi-instance cluster rollups.
    /// </summary>
    public static IReadOnlyList<IMetricSnapshot> AggregateSnapshots(this IEnumerable<IMetricSnapshot> snapshots)
    {
        var result = new List<IMetricSnapshot>();
        var grouped = snapshots.GroupBy(s => (s.MetricName, s.CounterName));

        foreach (var group in grouped)
        {
            var items = group.ToList();
            if (items.Count == 0) continue;

            if (items[0] is DurationSnapshot)
            {
                var durations = items.Cast<DurationSnapshot>().ToList();
                long inCount = 0;
                long outCount = 0;
                long failedCount = 0;
                long totalTicks = 0;
                long minTicks = long.MaxValue;
                long maxTicks = 0;
                var latestTimestamp = DateTime.MinValue;

                foreach (var d in durations)
                {
                    inCount += d.InCount;
                    outCount += d.OutCount;
                    failedCount += d.FailedCount;
                    totalTicks += d.TotalDuration.Ticks;

                    if (d.OutCount > 0)
                    {
                        if (d.MinDuration.Ticks > 0 && d.MinDuration.Ticks < minTicks)
                        {
                            minTicks = d.MinDuration.Ticks;
                        }
                        if (d.MaxDuration.Ticks > maxTicks)
                        {
                            maxTicks = d.MaxDuration.Ticks;
                        }
                    }

                    if (d.Timestamp > latestTimestamp)
                    {
                        latestTimestamp = d.Timestamp;
                    }
                }

                if (minTicks == long.MaxValue) minTicks = 0;

                var avg = outCount > 0 ? TimeSpan.FromTicks(totalTicks / outCount) : TimeSpan.Zero;

                result.Add(new DurationSnapshot(
                    MetricName: group.Key.MetricName,
                    CounterName: group.Key.CounterName,
                    InCount: inCount,
                    OutCount: outCount,
                    FailedCount: failedCount,
                    TotalDuration: TimeSpan.FromTicks(totalTicks),
                    AverageDuration: avg,
                    MinDuration: TimeSpan.FromTicks(minTicks),
                    MaxDuration: TimeSpan.FromTicks(maxTicks),
                    Timestamp: latestTimestamp == DateTime.MinValue ? DateTime.UtcNow : latestTimestamp
                ));
            }
            else if (items[0] is MemorySnapshot)
            {
                var memories = items.Cast<MemorySnapshot>().ToList();
                long opCount = 0;
                long totalBytes = 0;
                long minBytes = long.MaxValue;
                long maxBytes = 0;
                var latestTimestamp = DateTime.MinValue;

                foreach (var m in memories)
                {
                    opCount += m.OperationCount;
                    totalBytes += m.TotalAllocatedBytes;

                    if (m.OperationCount > 0)
                    {
                        if (m.MinAllocatedBytes > 0 && m.MinAllocatedBytes < minBytes)
                        {
                            minBytes = m.MinAllocatedBytes;
                        }
                        if (m.MaxAllocatedBytes > maxBytes)
                        {
                            maxBytes = m.MaxAllocatedBytes;
                        }
                    }

                    if (m.Timestamp > latestTimestamp)
                    {
                        latestTimestamp = m.Timestamp;
                    }
                }

                if (minBytes == long.MaxValue) minBytes = 0;

                var avgBytes = opCount > 0 ? totalBytes / opCount : 0;

                result.Add(new MemorySnapshot(
                    MetricName: group.Key.MetricName,
                    CounterName: group.Key.CounterName,
                    OperationCount: opCount,
                    TotalAllocatedBytes: totalBytes,
                    AverageAllocatedBytes: avgBytes,
                    MinAllocatedBytes: minBytes,
                    MaxAllocatedBytes: maxBytes,
                    Timestamp: latestTimestamp == DateTime.MinValue ? DateTime.UtcNow : latestTimestamp
                ));
            }
            else if (items[0] is ExceptionSnapshot)
            {
                var exceptions = items.Cast<ExceptionSnapshot>().ToList();
                long totalOps = 0;
                long totalFails = 0;
                var breakdown = new Dictionary<string, long>(StringComparer.Ordinal);
                var latestTimestamp = DateTime.MinValue;

                foreach (var e in exceptions)
                {
                    totalOps += e.TotalOperations;
                    totalFails += e.TotalFailures;

                    foreach (var (type, count) in e.ExceptionsByType)
                    {
                        breakdown[type] = breakdown.GetValueOrDefault(type) + count;
                    }

                    if (e.Timestamp > latestTimestamp)
                    {
                        latestTimestamp = e.Timestamp;
                    }
                }

                result.Add(new ExceptionSnapshot(
                    MetricName: group.Key.MetricName,
                    CounterName: group.Key.CounterName,
                    TotalOperations: totalOps,
                    TotalFailures: totalFails,
                    ExceptionsByType: breakdown,
                    Timestamp: latestTimestamp == DateTime.MinValue ? DateTime.UtcNow : latestTimestamp
                ));
            }
            else
            {
                // Unrecognized snapshot: keep latest
                result.Add(items[^1]);
            }
        }

        return result;
    }
}
