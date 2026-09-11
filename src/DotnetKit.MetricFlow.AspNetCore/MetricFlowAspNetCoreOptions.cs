using Microsoft.AspNetCore.Http;

namespace DotnetKit.MetricFlow.AspNetCore
{
    /// <summary>
    /// Configuration options for ASP.NET Core integration with MetricFlow.
    /// </summary>
    public class MetricFlowAspNetCoreOptions
    {
        /// <summary>
        /// The topic name assigned to the MetricTracker. Defaults to "AspNetCore".
        /// </summary>
        public string Topic { get; set; } = "AspNetCore";

        /// <summary>
        /// Optional static tags attached at the Topic level (e.g. environment, service name).
        /// </summary>
        public Dictionary<string, string>? TopicTags { get; set; }

        /// <summary>
        /// Sampling rate between 0.0 and 1.0 (or null to track 100%). Defaults to 1.0.
        /// </summary>
        public double? SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Strategy used to resolve the metric name from incoming requests.
        /// Defaults to <see cref="MetricRouteNamingStrategy.RoutePattern"/>.
        /// </summary>
        public MetricRouteNamingStrategy NamingStrategy { get; set; } = MetricRouteNamingStrategy.RoutePattern;

        /// <summary>
        /// Custom resolver function to determine the metric name for a request.
        /// When specified and returns a non-null/non-empty string, this takes precedence over <see cref="NamingStrategy"/>.
        /// </summary>
        public Func<HttpContext, string?>? RoutePatternResolver { get; set; }

        /// <summary>
        /// Fallback metric name when a request route cannot be matched or resolved.
        /// Defaults to "unmatched_route".
        /// </summary>
        public string FallbackRouteName { get; set; } = "unmatched_route";

        /// <summary>
        /// Whether to prefix the metric name with the HTTP method (e.g. "GET /weatherforecast").
        /// Defaults to false.
        /// </summary>
        public bool IncludeHttpMethodInMetricName { get; set; } = false;

        /// <summary>
        /// Exact request paths to exclude from metric tracking (case-insensitive).
        /// Defaults to common health and metric paths.
        /// </summary>
        public HashSet<string> ExcludedPaths { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "/metrics",
            "/health",
            "/healthz",
            "/ready",
            "/live",
            "/favicon.ico"
        };

        /// <summary>
        /// Path prefixes to exclude from metric tracking (e.g. "/swagger").
        /// </summary>
        public List<string> ExcludePathPrefixes { get; } = new();

        /// <summary>
        /// Optional custom predicate to determine if a request should be excluded from tracking.
        /// </summary>
        public Func<HttpContext, bool>? ExcludePredicate { get; set; }

        /// <summary>
        /// Optional hook to enrich metric tags with request-specific metadata (e.g. tenant_id, client_id).
        /// </summary>
        public Action<Dictionary<string, string>, HttpContext>? EnrichTags { get; set; }

        /// <summary>
        /// Whether to record the response HTTP status code as a tag (e.g. "http_status": "200").
        /// Defaults to true.
        /// </summary>
        public bool IncludeStatusCode { get; set; } = true;

        /// <summary>
        /// Whether to record the HTTP method as a tag (e.g. "http_method": "GET").
        /// Defaults to true.
        /// </summary>
        public bool IncludeHttpMethod { get; set; } = true;

        /// <summary>
        /// Whether to record the exception type name as a tag when an exception occurs.
        /// Defaults to true.
        /// </summary>
        public bool IncludeExceptionType { get; set; } = true;

        /// <summary>
        /// Whether to mark operations as failed if the HTTP status code is &gt;= 400.
        /// Defaults to true.
        /// </summary>
        public bool TrackFailedStatusCodes { get; set; } = true;
    }
}
