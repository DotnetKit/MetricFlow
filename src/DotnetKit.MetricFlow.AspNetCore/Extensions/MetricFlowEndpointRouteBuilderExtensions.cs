using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetKit.MetricFlow.AspNetCore.Extensions;

/// <summary>
/// Endpoint route builder extensions for exposing MetricFlow metrics endpoints.
/// </summary>
public static class MetricFlowEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an HTTP GET endpoint that outputs the current MetricFlow metrics snapshot as plain text.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the metrics endpoint. Defaults to "/metrics".</param>
    /// <returns>The route endpoint builder for additional configuration.</returns>
    public static RouteHandlerBuilder MapMetricFlow(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/metrics")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var options = endpoints.ServiceProvider.GetService<MetricFlowAspNetCoreOptions>();
        if (options != null && !options.ExcludedPaths.Contains(pattern))
        {
            options.ExcludedPaths.Add(pattern);
        }

        return endpoints.MapGet(pattern, (IMetricTracker tracker) =>
        {
            return Results.Text(tracker.ToString(), contentType: "text/plain; charset=utf-8");
        })
        .WithName("MetricFlow_Metrics");
    }

    /// <summary>
    /// Maps an HTTP GET endpoint that allows querying metric timeline snapshots and rollups across arbitrary [from, to] time ranges.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the timeline endpoint. Defaults to "/metrics/timeline".</param>
    /// <returns>The route endpoint builder for additional configuration.</returns>
    public static RouteHandlerBuilder MapMetricFlowTimeline(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/metrics/timeline")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var options = endpoints.ServiceProvider.GetService<MetricFlowAspNetCoreOptions>();
        if (options != null && !options.ExcludedPaths.Contains(pattern))
        {
            options.ExcludedPaths.Add(pattern);
        }

        return endpoints.MapGet(pattern, async (
            IMetricTimelineStore store,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? metric,
            bool? aggregated,
            CancellationToken cancellationToken) =>
        {
            var fromUtc = from ?? DateTimeOffset.UtcNow.AddHours(-1);
            var toUtc = to ?? DateTimeOffset.UtcNow;

            if (aggregated == true)
            {
                var aggregatedSnapshots = await store.GetAggregatedSnapshotsAsync(fromUtc, toUtc, metric, cancellationToken);
                return Results.Ok(aggregatedSnapshots);
            }

            var timeline = await store.GetTimelineAsync(fromUtc, toUtc, metric, cancellationToken);
            return Results.Ok(timeline);
        })
        .WithName("MetricFlow_Timeline");
    }
}
