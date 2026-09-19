using DotnetKit.MetricFlow.Sinks;
using Microsoft.Extensions.Hosting;

namespace DotnetKit.MetricFlow.AspNetCore;

/// <summary>
/// Background hosted service that manages the lifecycle of the <see cref="PeriodicMetricExporter"/>.
/// </summary>
public sealed class MetricFlowExporterHostedService : IHostedService
{
    private readonly PeriodicMetricExporter _exporter;

    public MetricFlowExporterHostedService(PeriodicMetricExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _exporter.Start();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _exporter.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
