using DotnetKit.MetricFlow.Abstractions;
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
}
