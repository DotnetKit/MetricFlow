using DotnetKit.MetricFlow.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;

namespace DotnetKit.MetricFlow.AspNetCore.Extensions;

/// <summary>
/// Application builder extensions for adding MetricFlow middleware.
/// </summary>
public static class MetricFlowApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the MetricFlow request tracking middleware to the application pipeline.
    /// Ensure this is placed after <c>UseRouting()</c> if explicit routing is used.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseMetricFlow(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<MetricFlowMiddleware>();
    }
}
