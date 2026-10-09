using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Counters;

namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Configuration options for <see cref="ConsoleMetricSink"/>.
/// </summary>
public class ConsoleMetricSinkOptions
{
    /// <summary>
    /// The <see cref="TextWriter"/> to write log output to. If null, defaults to <see cref="System.Console.Out"/>.
    /// Setting this to a custom writer is useful for unit tests or redirecting console streams.
    /// </summary>
    public TextWriter? OutputWriter { get; set; }

    /// <summary>
    /// Whether to enable ANSI color formatting in output. Defaults to true.
    /// </summary>
    public bool Colorize { get; set; } = true;

    /// <summary>
    /// Whether to include timestamps in log output. Defaults to true.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Custom timestamp format string. Defaults to "HH:mm:ss.fff".
    /// </summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss.fff";

    /// <summary>
    /// Whether to include topic-level tags in output. Defaults to true.
    /// </summary>
    public bool IncludeTopicTags { get; set; } = true;

    /// <summary>
    /// Optional prefix label to prepend to output lines (e.g. "[MetricFlow]"). Defaults to "[MetricFlow]".
    /// </summary>
    public string Prefix { get; set; } = "[MetricFlow]";

    private readonly List<Func<IMetricSnapshot, ConsoleColor?>> _thresholdRules = new();

    /// <summary>
    /// Gets or sets a custom delegate to dynamically select a <see cref="ConsoleColor"/> (or null to use default/threshold styling) for a given metric snapshot.
    /// When specified and returning a non-null color, this delegate takes precedence over registered threshold rules.
    /// </summary>
    public Func<IMetricSnapshot, ConsoleColor?>? ColorSelector { get; set; }

    /// <summary>
    /// Gets the list of registered threshold evaluation rules.
    /// </summary>
    public IReadOnlyList<Func<IMetricSnapshot, ConsoleColor?>> ThresholdRules => _thresholdRules;

    /// <summary>
    /// Clears all registered threshold rules.
    /// </summary>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions ClearThresholds()
    {
        _thresholdRules.Clear();
        return this;
    }

    /// <summary>
    /// Adds a custom threshold evaluation rule for snapshots.
    /// </summary>
    /// <param name="rule">A function that returns a <see cref="ConsoleColor"/> or null if no rule matched.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThresholdRule(Func<IMetricSnapshot, ConsoleColor?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _thresholdRules.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds a custom threshold rule for a specific snapshot type.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="rule">The evaluation delegate.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(Func<TSnapshot, ConsoleColor?> rule)
        where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(rule);
        return AddThresholdRule(snapshot => snapshot is TSnapshot typed ? rule(typed) : null);
    }

    /// <summary>
    /// Adds a threshold rule for a specific snapshot type using a <see cref="TimeSpan"/> value selector.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="valueSelector">Delegate extracting a <see cref="TimeSpan"/> from the snapshot.</param>
    /// <param name="warn">Warning latency threshold.</param>
    /// <param name="critical">Critical latency threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        Func<TSnapshot, TimeSpan> valueSelector,
        TimeSpan warn,
        TimeSpan critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        return AddThresholdRule(snapshot =>
        {
            if (snapshot is TSnapshot typed)
            {
                var value = valueSelector(typed);
                if (critical >= warn)
                {
                    if (value >= critical) return criticalColor;
                    if (value >= warn) return warnColor;
                }
                else
                {
                    if (value <= critical) return criticalColor;
                    if (value <= warn) return warnColor;
                }
                return normalColor;
            }
            return null;
        });
    }

    /// <summary>
    /// Adds a threshold rule for a snapshot type using its default <see cref="TimeSpan"/> metric
    /// (e.g. <see cref="DurationSnapshot.AverageDuration"/> for <see cref="DurationSnapshot"/>).
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="warn">Warning latency threshold.</param>
    /// <param name="critical">Critical latency threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        TimeSpan warn,
        TimeSpan critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        if (typeof(TSnapshot) == typeof(DurationSnapshot))
        {
            return AddThreshold<DurationSnapshot>(d => d.AverageDuration, warn, critical, warnColor, criticalColor, normalColor);
        }

        if (typeof(TSnapshot) == typeof(ThroughputSnapshot))
        {
            return AddThreshold<ThroughputSnapshot>(tp => tp.TotalDuration, warn, critical, warnColor, criticalColor, normalColor);
        }

        throw new NotSupportedException(
            $"Snapshot type '{typeof(TSnapshot).Name}' does not have a default TimeSpan selector. Please use the overload accepting a valueSelector delegate.");
    }

    /// <summary>
    /// Adds a threshold rule for a specific snapshot type using a <see cref="double"/> value selector.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="valueSelector">Delegate extracting a numeric value from the snapshot.</param>
    /// <param name="warn">Warning threshold.</param>
    /// <param name="critical">Critical threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        Func<TSnapshot, double> valueSelector,
        double warn,
        double critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        return AddThresholdRule(snapshot =>
        {
            if (snapshot is TSnapshot typed)
            {
                var value = valueSelector(typed);
                if (critical >= warn)
                {
                    if (value >= critical) return criticalColor;
                    if (value >= warn) return warnColor;
                }
                else
                {
                    if (value <= critical) return criticalColor;
                    if (value <= warn) return warnColor;
                }
                return normalColor;
            }
            return null;
        });
    }

    /// <summary>
    /// Adds a threshold rule for a snapshot type using its default <see cref="double"/> metric
    /// (e.g. <see cref="FailureSnapshot.FailureRate"/> for <see cref="FailureSnapshot"/> or <see cref="ExceptionSnapshot.ExceptionRate"/> for <see cref="ExceptionSnapshot"/>).
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="warn">Warning threshold.</param>
    /// <param name="critical">Critical threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        double warn,
        double critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        if (typeof(TSnapshot) == typeof(FailureSnapshot))
        {
            return AddThreshold<FailureSnapshot>(f => f.FailureRate, warn, critical, warnColor, criticalColor, normalColor);
        }

        if (typeof(TSnapshot) == typeof(ExceptionSnapshot))
        {
            return AddThreshold<ExceptionSnapshot>(ex => ex.ExceptionRate, warn, critical, warnColor, criticalColor, normalColor);
        }

        if (typeof(TSnapshot) == typeof(ThroughputSnapshot))
        {
            return AddThreshold<ThroughputSnapshot>(tp => tp.ItemsPerSecond, warn, critical, warnColor, criticalColor, normalColor);
        }

        throw new NotSupportedException(
            $"Snapshot type '{typeof(TSnapshot).Name}' does not have a default double selector. Please use the overload accepting a valueSelector delegate.");
    }

    /// <summary>
    /// Adds a threshold rule for a specific snapshot type using a <see cref="long"/> value selector.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="valueSelector">Delegate extracting a 64-bit integer metric from the snapshot.</param>
    /// <param name="warn">Warning threshold.</param>
    /// <param name="critical">Critical threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        Func<TSnapshot, long> valueSelector,
        long warn,
        long critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        return AddThresholdRule(snapshot =>
        {
            if (snapshot is TSnapshot typed)
            {
                var value = valueSelector(typed);
                if (critical >= warn)
                {
                    if (value >= critical) return criticalColor;
                    if (value >= warn) return warnColor;
                }
                else
                {
                    if (value <= critical) return criticalColor;
                    if (value <= warn) return warnColor;
                }
                return normalColor;
            }
            return null;
        });
    }

    /// <summary>
    /// Adds a threshold rule for a snapshot type using its default <see cref="long"/> metric
    /// (e.g. <see cref="ExceptionSnapshot.TotalExceptions"/> for <see cref="ExceptionSnapshot"/>, <see cref="FailureSnapshot.TotalFailures"/> for <see cref="FailureSnapshot"/>, or <see cref="MemorySnapshot.AverageAllocatedBytes"/> for <see cref="MemorySnapshot"/>).
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot to evaluate.</typeparam>
    /// <param name="warn">Warning threshold.</param>
    /// <param name="critical">Critical threshold.</param>
    /// <param name="warnColor">Color when warning threshold is reached. Defaults to <see cref="ConsoleColor.DarkYellow"/>.</param>
    /// <param name="criticalColor">Color when critical threshold is reached. Defaults to <see cref="ConsoleColor.Red"/>.</param>
    /// <param name="normalColor">Optional color when below thresholds. Defaults to <see cref="ConsoleColor.Green"/>.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddThreshold<TSnapshot>(
        long warn,
        long critical,
        ConsoleColor warnColor = ConsoleColor.DarkYellow,
        ConsoleColor criticalColor = ConsoleColor.Red,
        ConsoleColor? normalColor = ConsoleColor.Green)
        where TSnapshot : class, IMetricSnapshot
    {
        if (typeof(TSnapshot) == typeof(ExceptionSnapshot))
        {
            return AddThreshold<ExceptionSnapshot>(ex => ex.TotalExceptions, warn, critical, warnColor, criticalColor, normalColor);
        }

        if (typeof(TSnapshot) == typeof(FailureSnapshot))
        {
            return AddThreshold<FailureSnapshot>(f => f.TotalFailures, warn, critical, warnColor, criticalColor, normalColor);
        }

        if (typeof(TSnapshot) == typeof(MemorySnapshot))
        {
            return AddThreshold<MemorySnapshot>(m => m.AverageAllocatedBytes, warn, critical, warnColor, criticalColor, normalColor);
        }

        throw new NotSupportedException(
            $"Snapshot type '{typeof(TSnapshot).Name}' does not have a default long selector. Please use the overload accepting a valueSelector delegate.");
    }

    /// <summary>
    /// Resolves the effective <see cref="ConsoleColor"/> for the specified snapshot.
    /// Evaluates <see cref="ColorSelector"/> first, then registered threshold rules in registration order.
    /// Returns null if no custom rule applies.
    /// </summary>
    /// <param name="snapshot">The snapshot to evaluate.</param>
    /// <returns>The resolved color, or null for default formatting.</returns>
    public ConsoleColor? ResolveColor(IMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (ColorSelector != null)
        {
            var selected = ColorSelector(snapshot);
            if (selected.HasValue)
            {
                return selected.Value;
            }
        }

        for (int i = 0; i < _thresholdRules.Count; i++)
        {
            var color = _thresholdRules[i](snapshot);
            if (color.HasValue)
            {
                return color.Value;
            }
        }

        return null;
    }
}
