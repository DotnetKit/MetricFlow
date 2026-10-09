using System.Collections;
using DotnetKit.MetricFlow.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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
    public IMetricFlowBuilder AddConsoleSink(Action<DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSinkOptions>? configure = null)
    {
        var options = new DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSinkOptions();
        configure?.Invoke(options);
        return AddSink(new DotnetKit.MetricFlow.Sinks.Console.ConsoleMetricSink(options));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddLoggerSink(Action<DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSinkOptions>? configure = null)
    {
        Current.AddSingleton<DotnetKit.MetricFlow.Sinks.IMetricSink>(sp =>
        {
            var options = new DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSinkOptions();
            configure?.Invoke(options);
            var logger = options.Logger
                ?? sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(options.CategoryName)
                ?? sp.GetService<Microsoft.Extensions.Logging.ILogger>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            return new DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSink(logger, options);
        });
        return this;
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddLoggerSink(Microsoft.Extensions.Logging.ILogger logger, Action<DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSinkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var options = new DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSinkOptions();
        configure?.Invoke(options);
        return AddSink(new DotnetKit.MetricFlow.Sinks.Logger.LoggerMetricSink(logger, options));
    }

    /// <inheritdoc />
    public IMetricFlowBuilder AddSink(DotnetKit.MetricFlow.Sinks.IMetricSink sink)
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
