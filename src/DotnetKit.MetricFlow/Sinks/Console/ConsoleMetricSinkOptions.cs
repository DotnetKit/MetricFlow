using System.Globalization;
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

    /// <summary>
    /// Whether to render hierarchical parent-child execution trees when a hierarchy snapshot is emitted. Defaults to true.
    /// </summary>
    public bool ShowHierarchicalTree { get; set; } = true;

    /// <summary>
    /// The unit used to format durations. Defaults to <see cref="DurationUnit.Milliseconds"/>.
    /// </summary>
    public DurationUnit DurationUnit { get; set; } = DurationUnit.Milliseconds;

    /// <summary>
    /// The number of decimal places used when formatting durations. Defaults to 2.
    /// </summary>
    public int DurationDecimals { get; set; } = 2;

    /// <summary>
    /// Custom delegate to format <see cref="TimeSpan"/> durations. When specified, this overrides <see cref="DurationUnit"/>.
    /// </summary>
    public Func<TimeSpan, string>? DurationFormatter { get; set; }

    /// <summary>
    /// The unit label displayed for throughput rates. Defaults to "items/s".
    /// Can be customized to domain units like "req/s", "msg/s", "orders/s", etc.
    /// </summary>
    public string ThroughputUnit { get; set; } = "items/s";

    /// <summary>
    /// The unit used to format memory allocations. Defaults to <see cref="MemoryUnit.Auto"/>.
    /// </summary>
    public MemoryUnit MemoryUnit { get; set; } = MemoryUnit.Auto;

    /// <summary>
    /// The number of decimal places used when formatting memory byte values. Defaults to 2.
    /// </summary>
    public int MemoryDecimals { get; set; } = 2;

    /// <summary>
    /// Custom delegate to format memory byte values. When specified, this overrides <see cref="MemoryUnit"/>.
    /// </summary>
    public Func<long, string>? MemoryFormatter { get; set; }

    private readonly List<Func<IMetricSnapshot, ConsoleColor?>> _thresholdRules = new();
    private readonly Dictionary<Type, Func<IMetricSnapshot, string>> _typedFormatters = new();
    private readonly Dictionary<string, Func<IMetricSnapshot, string>> _namedFormatters = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> duration according to configured <see cref="DurationUnit"/>, <see cref="DurationDecimals"/>, or <see cref="DurationFormatter"/>.
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>The formatted duration string.</returns>
    public string FormatDuration(TimeSpan duration)
    {
        if (DurationFormatter != null)
        {
            return DurationFormatter(duration);
        }

        var culture = CultureInfo.InvariantCulture;
        var format = $"F{DurationDecimals}";

        return DurationUnit switch
        {
            DurationUnit.Milliseconds => $"{duration.TotalMilliseconds.ToString(format, culture)} ms",
            DurationUnit.Seconds => $"{duration.TotalSeconds.ToString(format, culture)} s",
            DurationUnit.Minutes => $"{duration.TotalMinutes.ToString(format, culture)} m",
            DurationUnit.Hours => $"{duration.TotalHours.ToString(format, culture)} h",
            DurationUnit.Auto => FormatDurationAuto(duration, format, culture),
            _ => $"{duration.TotalMilliseconds.ToString(format, culture)} ms"
        };
    }

    private static string FormatDurationAuto(TimeSpan duration, string format, CultureInfo culture)
    {
        if (duration.TotalMilliseconds < 1000)
        {
            return $"{duration.TotalMilliseconds.ToString(format, culture)} ms";
        }
        if (duration.TotalSeconds < 60)
        {
            return $"{duration.TotalSeconds.ToString(format, culture)} s";
        }
        if (duration.TotalMinutes < 60)
        {
            return $"{duration.TotalMinutes.ToString(format, culture)} m";
        }
        return $"{duration.TotalHours.ToString(format, culture)} h";
    }

    /// <summary>
    /// Formats a byte count according to configured <see cref="MemoryUnit"/>, <see cref="MemoryDecimals"/>, or <see cref="MemoryFormatter"/>.
    /// </summary>
    /// <param name="bytes">The byte count to format.</param>
    /// <returns>The formatted memory string.</returns>
    public string FormatMemory(long bytes)
    {
        if (MemoryFormatter != null)
        {
            return MemoryFormatter(bytes);
        }

        var culture = CultureInfo.InvariantCulture;
        var format = $"F{MemoryDecimals}";

        return MemoryUnit switch
        {
            MemoryUnit.Bytes => $"{bytes} B",
            MemoryUnit.Kilobytes => $"{(bytes / 1024.0).ToString(format, culture)} KB",
            MemoryUnit.Megabytes => $"{(bytes / (1024.0 * 1024.0)).ToString(format, culture)} MB",
            MemoryUnit.Gigabytes => $"{(bytes / (1024.0 * 1024.0 * 1024.0)).ToString(format, culture)} GB",
            _ => FormatMemoryAuto(bytes, format, culture)
        };
    }

    private static string FormatMemoryAuto(long bytes, string format, CultureInfo culture)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{(bytes / 1024.0).ToString(format, culture)} KB";
        }
        if (bytes < 1024 * 1024 * 1024)
        {
            return $"{(bytes / (1024.0 * 1024.0)).ToString(format, culture)} MB";
        }
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)).ToString(format, culture)} GB";
    }

    /// <summary>
    /// Registers a custom formatting delegate for snapshots of type <typeparamref name="TSnapshot"/>.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of metric snapshot to format.</typeparam>
    /// <param name="formatter">The custom formatting delegate.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddFormatter<TSnapshot>(Func<TSnapshot, string> formatter)
        where TSnapshot : class, IMetricSnapshot
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _typedFormatters[typeof(TSnapshot)] = snap => formatter((TSnapshot)snap);
        return this;
    }

    /// <summary>
    /// Registers a custom formatting delegate for snapshots matching a specific counter name.
    /// </summary>
    /// <param name="counterName">The counter name to match.</param>
    /// <param name="formatter">The custom formatting delegate.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public ConsoleMetricSinkOptions AddFormatter(string counterName, Func<IMetricSnapshot, string> formatter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterName);
        ArgumentNullException.ThrowIfNull(formatter);
        _namedFormatters[counterName] = formatter;
        return this;
    }

    /// <summary>
    /// Attempts to format a metric snapshot using registered custom formatters.
    /// </summary>
    /// <param name="snapshot">The snapshot to format.</param>
    /// <param name="formatted">The formatted string if a custom formatter was found, otherwise null.</param>
    /// <returns>True if a custom formatter was applied, false otherwise.</returns>
    public bool TryFormatCustom(IMetricSnapshot snapshot, out string? formatted)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (_namedFormatters.TryGetValue(snapshot.CounterName, out var named))
        {
            formatted = named(snapshot);
            return true;
        }

        if (_typedFormatters.TryGetValue(snapshot.GetType(), out var typed))
        {
            formatted = typed(snapshot);
            return true;
        }

        formatted = null;
        return false;
    }
}
