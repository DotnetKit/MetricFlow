using DotnetKit.MetricFlow.Tracker.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DotnetKit.MetricFlow.AspNetCore.Middleware
{
    /// <summary>
    /// Middleware for tracking HTTP request rate, duration, errors, and status codes using MetricFlow.
    /// </summary>
    public class MetricFlowMiddleware
    {
        private const string MetricNameItemKey = "__MetricFlow_MetricName";

        private readonly RequestDelegate _next;
        private readonly MetricFlowAspNetCoreOptions _options;
        private readonly IMetricTracker _tracker;

        public MetricFlowMiddleware(
            RequestDelegate next,
            MetricFlowAspNetCoreOptions options,
            IMetricTracker tracker)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldExclude(context))
            {
                await _next(context);
                return;
            }

            string metricName = ResolveMetricName(context);
            context.Items[MetricNameItemKey] = metricName;

            var inTags = CreateTags(context);
            _tracker.In(metricName, inTags);

            bool failed = false;
            Exception? caughtException = null;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                failed = true;
                caughtException = ex;
                throw;
            }
            finally
            {
                if (!failed && _options.TrackFailedStatusCodes && context.Response.StatusCode >= 400)
                {
                    failed = true;
                }

                var outTags = CreateTags(context);
                if (_options.IncludeStatusCode)
                {
                    outTags["http_status"] = context.Response.StatusCode.ToString();
                }

                if (caughtException != null && _options.IncludeExceptionType)
                {
                    outTags["error_type"] = caughtException.GetType().Name;
                }

                // Allow late enrichment of tags that may have been established during request execution
                _options.EnrichTags?.Invoke(outTags, context);

                _tracker.Out(
                    metricName,
                    tags: outTags,
                    failed: failed,
                    exception: caughtException);
            }
        }

        private bool ShouldExclude(HttpContext context)
        {
            var path = context.Request.Path;
            var pathValue = path.Value;

            if (!string.IsNullOrEmpty(pathValue) && _options.ExcludedPaths.Contains(pathValue))
            {
                return true;
            }

            for (int i = 0; i < _options.ExcludePathPrefixes.Count; i++)
            {
                if (path.StartsWithSegments(_options.ExcludePathPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (_options.ExcludePredicate != null && _options.ExcludePredicate(context))
            {
                return true;
            }

            return false;
        }

        private string ResolveMetricName(HttpContext context)
        {
            if (_options.RoutePatternResolver != null)
            {
                var custom = _options.RoutePatternResolver(context);
                if (!string.IsNullOrEmpty(custom))
                {
                    return FormatWithMethod(custom, context);
                }
            }

            var endpoint = context.GetEndpoint();
            string? name = null;

            switch (_options.NamingStrategy)
            {
                case MetricRouteNamingStrategy.EndpointName:
                    name = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                           ?? endpoint?.DisplayName;
                    if (string.IsNullOrEmpty(name) && endpoint is RouteEndpoint routeEndpoint1)
                    {
                        name = NormalizeRoutePattern(routeEndpoint1.RoutePattern.RawText);
                    }
                    break;

                case MetricRouteNamingStrategy.RawPath:
                    name = context.Request.Path.Value;
                    break;

                case MetricRouteNamingStrategy.RoutePattern:
                default:
                    if (endpoint is RouteEndpoint routeEndpoint2)
                    {
                        name = NormalizeRoutePattern(routeEndpoint2.RoutePattern.RawText);
                    }
                    break;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = _options.FallbackRouteName;
            }

            return FormatWithMethod(name, context);
        }

        private Dictionary<string, string> CreateTags(HttpContext context)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (_options.IncludeHttpMethod)
            {
                tags["http_method"] = context.Request.Method;
            }

            _options.EnrichTags?.Invoke(tags, context);
            return tags;
        }

        private static string NormalizeRoutePattern(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return "/";
            }

            return rawText.StartsWith('/') ? rawText : "/" + rawText;
        }

        private string FormatWithMethod(string name, HttpContext context)
        {
            if (_options.IncludeHttpMethodInMetricName)
            {
                return $"{context.Request.Method} {name}";
            }

            return name;
        }
    }
}
