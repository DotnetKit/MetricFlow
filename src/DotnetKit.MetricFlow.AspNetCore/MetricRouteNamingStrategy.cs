namespace DotnetKit.MetricFlow.AspNetCore;

/// <summary>
/// Defines how the metric name is resolved from an incoming HTTP request.
/// </summary>
public enum MetricRouteNamingStrategy
{
    /// <summary>
    /// Uses the matched route pattern (e.g. "/weatherforecast", "/users/{id}").
    /// Recommended to prevent cardinality explosion.
    /// </summary>
    RoutePattern,

    /// <summary>
    /// Uses the endpoint name or display name metadata (e.g. from .WithName("GetWeatherForecast")).
    /// Falls back to route pattern if no name is available.
    /// </summary>
    EndpointName,

    /// <summary>
    /// Uses the raw Request.Path (e.g. "/users/123").
    /// CAUTION: May lead to high cardinality if paths contain dynamic identifiers.
    /// </summary>
    RawPath
}
