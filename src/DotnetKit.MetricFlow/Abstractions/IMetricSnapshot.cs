namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Represents an immutable point-in-time snapshot of recorded metric values.
/// </summary>
public interface IMetricSnapshot
{
    /// <summary>
    /// Gets the name of the metric or tracked operation.
    /// </summary>
    string MetricName { get; }

    /// <summary>
    /// Gets the name of the counter that produced this snapshot (e.g. "Duration", "Throughput").
    /// </summary>
    string CounterName { get; }

    /// <summary>
    /// Gets the UTC timestamp when this snapshot was captured.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Formats the snapshot values into a human-readable string suitable for logging or console output.
    /// </summary>
    /// <returns>A formatted representation of the snapshot metrics.</returns>
    string ToFormattedString();
}

