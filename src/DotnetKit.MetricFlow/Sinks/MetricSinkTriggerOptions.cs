namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// Configures execution-lifecycle sampling triggers for emitting metric snapshots to sinks without requiring a background timer.
/// </summary>
public class MetricSinkTriggerOptions
{
    /// <summary>
    /// Emit snapshots every N executions of a metric (e.g., 50 or 100). Default is null (disabled).
    /// </summary>
    public int? EmitEveryNExecutions { get; set; }

    /// <summary>
    /// Emit snapshots immediately when an operation fails or throws an exception. Default is false.
    /// </summary>
    public bool EmitOnFailure { get; set; }

    /// <summary>
    /// Emit snapshots immediately when an operation duration exceeds this threshold. Default is null (disabled).
    /// </summary>
    public TimeSpan? EmitOnSlowDurationThreshold { get; set; }

    /// <summary>
    /// Probabilistic sampling rate between 0.0 and 1.0 (e.g. 0.05 for 5% of operations). Default is null (disabled).
    /// </summary>
    public double? EmitSampleRate { get; set; }

    /// <summary>
    /// Gets whether any trigger condition is configured.
    /// </summary>
    public bool HasTriggers =>
        EmitEveryNExecutions.HasValue ||
        EmitOnFailure ||
        EmitOnSlowDurationThreshold.HasValue ||
        EmitSampleRate.HasValue;

    /// <summary>
    /// Determines whether the sink should be triggered based on the operation context and current execution count.
    /// </summary>
    /// <param name="executionCount">The updated total execution count for this metric.</param>
    /// <param name="failed">Whether the operation failed or encountered an exception.</param>
    /// <param name="duration">The duration of the operation.</param>
    /// <returns>True if snapshots should be emitted to registered sinks; otherwise, false.</returns>
    public bool ShouldTrigger(long executionCount, bool failed, TimeSpan? duration)
    {
        if (EmitOnFailure && failed)
        {
            return true;
        }

        if (EmitOnSlowDurationThreshold.HasValue && duration.HasValue && duration.Value >= EmitOnSlowDurationThreshold.Value)
        {
            return true;
        }

        if (EmitEveryNExecutions.HasValue && EmitEveryNExecutions.Value > 0 && executionCount % EmitEveryNExecutions.Value == 0)
        {
            return true;
        }

        if (EmitSampleRate.HasValue && EmitSampleRate.Value > 0)
        {
            if (EmitSampleRate.Value >= 1.0 || Random.Shared.NextDouble() < EmitSampleRate.Value)
            {
                return true;
            }
        }

        return false;
    }
}
