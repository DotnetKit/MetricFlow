using System.Collections;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Sinks;
using DotnetKit.MetricFlow.Sinks.Console;
using DotnetKit.MetricFlow.Sinks.Logger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Fluent builder implementation for configuring MetricFlow and topic trackers.
/// </summary>
public class MetricFlowBuilder : IMetricFlowBuilder
{
    /// <inheritdoc />
    public IServiceCollection Current { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MetricFlowBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public MetricFlowBuilder(IServiceCollection services)
    {
        Current = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddMetricTracker(string topic, Action<MetricFlowOptions>? configure = null)
    {
        Current.AddMetricFlowTracker(topic, configure);
        return this;
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddMetricTracker(Action<MetricFlowOptions>? configure = null)
    {
        Current.AddMetricFlowTracker(configure);
        return this;
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddConsoleSink(Action<ConsoleMetricSinkOptions>? configure = null)
    {
        var options = new ConsoleMetricSinkOptions();
        configure?.Invoke(options);
        return AddSink(new ConsoleMetricSink(options));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddConsoleSink(TextWriter outputWriter, Action<ConsoleMetricSinkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(outputWriter);
        var options = new ConsoleMetricSinkOptions
        {
            OutputWriter = outputWriter
        };
        configure?.Invoke(options);
        return AddSink(new ConsoleMetricSink(options));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddLoggerSink(Action<LoggerMetricSinkOptions>? configure = null)
    {
        Current.AddSingleton<IMetricSink>(sp =>
        {
            var options = new LoggerMetricSinkOptions();
            configure?.Invoke(options);
            var logger = options.Logger
                ?? sp.GetService<ILoggerFactory>()?.CreateLogger(options.CategoryName)
                ?? sp.GetService<ILogger>()
                ?? NullLogger.Instance;
            return new LoggerMetricSink(logger, options);
        });
        return this;
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddLoggerSink(ILogger logger, Action<LoggerMetricSinkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var options = new LoggerMetricSinkOptions();
        configure?.Invoke(options);
        return AddSink(new LoggerMetricSink(logger, options));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddSink(IMetricSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        Current.AddSingleton(sink);
        return this;
    }


    #region IServiceCollection delegation

    int ICollection<ServiceDescriptor>.Count => Current.Count;
    bool ICollection<ServiceDescriptor>.IsReadOnly => Current.IsReadOnly;
    ServiceDescriptor IList<ServiceDescriptor>.this[int index] { get => Current[index]; set => Current[index] = value; }
    void ICollection<ServiceDescriptor>.Add(ServiceDescriptor item) => Current.Add(item);
    void ICollection<ServiceDescriptor>.Clear() => Current.Clear();
    bool ICollection<ServiceDescriptor>.Contains(ServiceDescriptor item) => Current.Contains(item);
    void ICollection<ServiceDescriptor>.CopyTo(ServiceDescriptor[] array, int arrayIndex) => Current.CopyTo(array, arrayIndex);
    bool ICollection<ServiceDescriptor>.Remove(ServiceDescriptor item) => Current.Remove(item);
    IEnumerator<ServiceDescriptor> IEnumerable<ServiceDescriptor>.GetEnumerator() => Current.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Current.GetEnumerator();
    int IList<ServiceDescriptor>.IndexOf(ServiceDescriptor item) => Current.IndexOf(item);
    void IList<ServiceDescriptor>.Insert(int index, ServiceDescriptor item) => Current.Insert(index, item);
    void IList<ServiceDescriptor>.RemoveAt(int index) => Current.RemoveAt(index);

    #endregion
}
