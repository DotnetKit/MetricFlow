namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Configuration options for duration measurement and percentile calculation.
/// </summary>
public class DurationCounterOptions
{
    /// <summary>
    /// The default capacity of the sliding-window sample reservoir (1024 samples).
    /// </summary>
    public const int DefaultReservoirSize = 1024;

    /// <summary>
    /// The default quantiles calculated for duration snapshots (p50, p90, p95, p99).
    /// </summary>
    public static readonly double[] DefaultPercentiles = [0.50, 0.90, 0.95, 0.99];

    /// <summary>
    /// Gets or sets whether percentile calculation (p50, p90, p95, p99) is enabled.
    /// Defaults to true.
    /// </summary>
    public bool EnablePercentiles { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of recent samples maintained in the sliding reservoir.
    /// Defaults to 1024.
    /// </summary>
    public int ReservoirSize { get; set; } = DefaultReservoirSize;

    /// <summary>
    /// Gets or sets the quantiles to compute for the snapshot (e.g. 0.50, 0.90, 0.95, 0.99).
    /// </summary>
    public double[] Percentiles { get; set; } = DefaultPercentiles;
}
