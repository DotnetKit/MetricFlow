using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace DotnetKit.MetricFlow;

/// <summary>
/// Extension methods for configuring metric sinks and sink triggers directly on <see cref="IMetricTracker"/>.
/// </summary>
public static class TrackerSinkExtensions
{
    /// <summary>
    /// Registers a <see cref="ConsoleMetricSink"/> on the metric tracker for structured console logging of metric snapshots.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="configure">Optional configuration for console output formatting.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddConsoleSink<T>(
        this T tracker,
        Action<ConsoleMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        var options = new ConsoleMetricSinkOptions();
        configure?.Invoke(options);
        tracker.RegisterSink(new ConsoleMetricSink(options));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="ConsoleMetricSink"/> on the metric tracker for structured console logging of metric snapshots to a specified <see cref="TextWriter"/>.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="outputWriter">The target writer for log output.</param>
    /// <param name="configure">Optional configuration for console output formatting.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddConsoleSink<T>(
        this T tracker,
        TextWriter outputWriter,
        Action<ConsoleMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(outputWriter);
        var options = new ConsoleMetricSinkOptions
        {
            OutputWriter = outputWriter
        };
        configure?.Invoke(options);
        tracker.RegisterSink(new ConsoleMetricSink(options));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="LiveConsoleMetricSink"/> on the metric tracker for real-time cursor-managed multi-stage table updates.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="configure">Optional configuration for dashboard refresh interval, title, and formatting.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddLiveConsoleDashboard<T>(
        this T tracker,
        Action<LiveConsoleMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        var options = new LiveConsoleMetricSinkOptions();
        configure?.Invoke(options);
        tracker.RegisterSink(new LiveConsoleMetricSink(options));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> on the metric tracker for structured logging via <see cref="ILogger"/>.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddLoggerSink<T>(
        this T tracker,
        Action<LoggerMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        tracker.RegisterSink(new LoggerMetricSink(options));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> on the metric tracker for structured logging using a specific <see cref="ILogger"/>.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddLoggerSink<T>(
        this T tracker,
        ILogger logger,
        Action<LoggerMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(logger);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        tracker.RegisterSink(new LoggerMetricSink(logger, options));
        return tracker;
    }

    /// <summary>
    /// Registers a <see cref="LoggerMetricSink"/> on the metric tracker for structured logging using an <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="loggerFactory">The logger factory instance.</param>
    /// <param name="configure">Optional configuration for logger output formatting and log levels.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddLoggerSink<T>(
        this T tracker,
        ILoggerFactory loggerFactory,
        Action<LoggerMetricSinkOptions>? configure = null)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        tracker.RegisterSink(new LoggerMetricSink(loggerFactory, options));
        return tracker;
    }

    /// <summary>
    /// Registers an <see cref="ObservableMetricSink"/> on the metric tracker for reactive streaming of metric snapshots via <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="observable">Outputs the created observable sink for subscribing.</param>
    /// <param name="name">Optional sink name. Defaults to "Observable".</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T AddObservableSink<T>(
        this T tracker,
        out ObservableMetricSink observable,
        string name = "Observable")
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        observable = new ObservableMetricSink(name);
        tracker.RegisterSink(observable);
        return tracker;
    }

    /// <summary>
    /// Configures execution-lifecycle sampling triggers for emitting metric snapshots to sinks without requiring a timer.
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T ConfigureSinkTriggers<T>(
        this T tracker,
        Action<MetricSinkTriggerOptions> configure)
        where T : IMetricTracker
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(configure);
        if (tracker.SinkTriggers is { } triggers)
        {
            configure(triggers);
        }
        return tracker;
    }

    /// <summary>
    /// Configures execution-lifecycle sampling triggers for emitting metric snapshots to sinks without requiring a timer (alias for <see cref="ConfigureSinkTriggers{T}"/>).
    /// </summary>
    /// <typeparam name="T">The metric tracker type.</typeparam>
    /// <param name="tracker">The tracker instance.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The same tracker instance for method chaining.</returns>
    public static T ConfigureSinkSampling<T>(
        this T tracker,
        Action<MetricSinkTriggerOptions> configure)
        where T : IMetricTracker
        => tracker.ConfigureSinkTriggers(configure);

    /// <summary>
    /// Starts a background timer that periodically flushes all active snapshots to registered sinks.
    /// </summary>
    /// <param name="tracker">The metric tracker instance.</param>
    /// <param name="interval">The time interval between periodic flushes.</param>
    /// <param name="dueTime">Optional initial delay before the first periodic flush. Defaults to <paramref name="interval"/>.</param>
    /// <returns>An <see cref="IDisposable"/> token that stops and cleans up the periodic flush timer when disposed.</returns>
    public static IDisposable EnablePeriodicFlush(
        this IMetricTracker tracker,
        TimeSpan interval,
        TimeSpan? dueTime = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }

        return new PeriodicFlushRegistration(tracker, interval, dueTime ?? interval);
    }

    private sealed class PeriodicFlushRegistration : IDisposable
    {
        private readonly IMetricTracker _tracker;
        private readonly Timer _timer;
        private int _isFlushing;
        private bool _disposed;

        public PeriodicFlushRegistration(IMetricTracker tracker, TimeSpan interval, TimeSpan dueTime)
        {
            _tracker = tracker;
            _timer = new Timer(OnTick, null, dueTime, interval);
        }

        private void OnTick(object? state)
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0) return;

            try
            {
                _tracker.FlushSinks();
            }
            catch
            {
                // Sinks must not crash the periodic timer callback
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer.Dispose();

            try
            {
                _tracker.FlushSinks();
            }
            catch
            {
                // Sinks must not throw during final flush on dispose
            }
        }
    }
}
