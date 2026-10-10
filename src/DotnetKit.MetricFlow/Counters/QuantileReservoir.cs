using System;
using System.Collections.Generic;
using System.Threading;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// A high-performance, bounded sliding-window reservoir for estimating latency percentiles and quantiles.
/// Uses a circular buffer to record the most recent N samples with minimal overhead and lock-free writes.
/// </summary>
public sealed class QuantileReservoir
{
    private readonly long[] _samples;
    private readonly int _capacity;
    private long _count;

    /// <summary>
    /// Gets the capacity (maximum number of recent samples) of the reservoir.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the total number of recorded observations since creation or last reset.
    /// </summary>
    public long TotalCount => Interlocked.Read(ref _count);

    /// <summary>
    /// Initializes a new instance of <see cref="QuantileReservoir"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of recent samples to maintain. Defaults to 1024.</param>
    public QuantileReservoir(int capacity = DurationCounterOptions.DefaultReservoirSize)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _samples = new long[capacity];
    }

    /// <summary>
    /// Records a duration sample (in stopwatch ticks) into the sliding reservoir.
    /// </summary>
    /// <param name="ticks">The duration in ticks.</param>
    public void Record(long ticks)
    {
        if (ticks < 0) ticks = 0;

        var index = Interlocked.Increment(ref _count) - 1;
        var slot = (int)((ulong)index % (ulong)_capacity);
        _samples[slot] = ticks;
    }

    /// <summary>
    /// Resets the reservoir.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
        Array.Clear(_samples, 0, _samples.Length);
    }

    /// <summary>
    /// Calculates percentiles for the specified quantiles based on the current sample window.
    /// </summary>
    /// <param name="quantiles">The quantiles to compute (e.g. 0.50, 0.90, 0.95, 0.99).</param>
    /// <returns>A dictionary of quantiles and their calculated TimeSpan values.</returns>
    public IReadOnlyDictionary<double, TimeSpan> GetPercentiles(IReadOnlyCollection<double> quantiles)
    {
        ArgumentNullException.ThrowIfNull(quantiles);

        var total = Interlocked.Read(ref _count);
        var count = (int)Math.Min(total, (long)_capacity);

        if (count == 0 || quantiles.Count == 0)
        {
            var empty = new Dictionary<double, TimeSpan>(quantiles.Count);
            foreach (var q in quantiles)
            {
                empty[q] = TimeSpan.Zero;
            }
            return empty;
        }

        var copy = new long[count];
        Array.Copy(_samples, copy, count);
        Array.Sort(copy);

        var results = new Dictionary<double, TimeSpan>(quantiles.Count);
        foreach (var q in quantiles)
        {
            var ticks = CalculateQuantile(copy, q);
            results[q] = TimeSpan.FromTicks((long)Math.Round(ticks));
        }

        return results;
    }

    /// <summary>
    /// Calculates a specific quantile from sorted sample ticks using linear interpolation between closest ranks.
    /// </summary>
    public static double CalculateQuantile(long[] sortedSamples, double quantile)
    {
        if (sortedSamples.Length == 0) return 0;
        if (sortedSamples.Length == 1) return sortedSamples[0];
        if (quantile <= 0.0) return sortedSamples[0];
        if (quantile >= 1.0) return sortedSamples[^1];

        double pos = quantile * (sortedSamples.Length - 1);
        int lowerIndex = (int)pos;
        int upperIndex = lowerIndex + 1;
        double fraction = pos - lowerIndex;

        if (upperIndex >= sortedSamples.Length)
        {
            return sortedSamples[^1];
        }

        return sortedSamples[lowerIndex] + fraction * (sortedSamples[upperIndex] - sortedSamples[lowerIndex]);
    }
}
