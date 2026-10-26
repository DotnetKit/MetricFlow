# Web Implementation (ASP.NET Core)

The **Web Implementation** package ([`DotnetKit.MetricFlow.AspNetCore`](xref:DotnetKit.MetricFlow.AspNetCore)) provides turnkey middleware and endpoint routing for ASP.NET Core Web APIs, Minimal APIs, and MVC applications. It automatically instruments incoming HTTP requests, tracks latency, status codes, and exceptions, and exposes a formatted metrics endpoint.

---

## Best For

- **ASP.NET Core Web APIs & Minimal APIs**: Automatic request duration and throughput tracking per route pattern.
- **Microservices & Gateway Services**: Route-level performance monitoring and client/tenant header tag enrichment.
- **RESTful Endpoints & MVC Controllers**: Detailed status code breakdown (2xx, 4xx, 5xx) with zero boilerplate.
- **Health Check & Scrape Integration**: Expose application metrics via `/metrics` or Prometheus scrapers.

---

## Quick Start

Install `DotnetKit.MetricFlow.AspNetCore`, register services, and insert the middleware into your HTTP pipeline:

```csharp
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Register MetricFlow ASP.NET Core services with custom tag enrichers
builder.Services.AddMetricFlow("WebApi", options =>
{
    options.AddHttpTagsEnricher((tags, context) =>
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            tags["tenant_id"] = tenantId!;
        }
    });
});

var app = builder.Build();

// 2. Enable turnkey HTTP tracking middleware
app.UseMetricFlow();

// 3. Application routes
app.MapGet("/weatherforecast", (IMetricTracker tracker) =>
{
    // Profile sub-operations inside endpoint handlers
    using var scope = tracker.Track("GenerateForecastData");
    return Results.Ok(new[] { "Mild", "Warm", "Hot" });
});

// 4. Expose the metrics endpoint
app.MapMetricFlow("/metrics");

app.Run();
```

---

## Available Options & API Reference

### 1. `MetricFlowAspNetCoreOptions`

[`MetricFlowAspNetCoreOptions`](xref:DotnetKit.MetricFlow.AspNetCore.MetricFlowAspNetCoreOptions) extends [`MetricFlowOptions`](xref:DotnetKit.MetricFlow.MetricFlowOptions) with ASP.NET Core-specific pipeline controls:

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `NamingStrategy` | `MetricRouteNamingStrategy` | `RoutePattern` | Strategy to determine metric names (`RoutePattern`, `Path`, `EndpointName`). |
| `RoutePatternResolver` | `Func<HttpContext, string?>?` | `null` | Custom delegate to determine metric names. Takes precedence over `NamingStrategy`. |
| `FallbackRouteName` | `string` | `"unmatched_route"` | Metric name used when route resolution produces no match. |
| `IncludeHttpMethodInMetricName`| `bool` | `false` | When true, prefixes metric names with HTTP method (e.g. `"GET /weatherforecast"`). |
| `ExcludedPaths` | `HashSet<string>` | `["/metrics", "/health", ...]` | Exact request paths to exclude from tracking (case-insensitive). |
| `ExcludePathPrefixes` | `List<string>` | `[]` | Path prefixes to ignore (e.g. `["/swagger"]`). |
| `ExcludePredicate` | `Func<HttpContext, bool>?` | `null` | Custom lambda predicate to dynamically skip tracking. |
| `EnrichTags` | `Action<Dictionary, HttpContext>?` | `null` | Delegate to enrich tags using the incoming `HttpContext`. |
| `AddHttpTagsEnricher(...)` | Method | Fluent | Chains HTTP tag enrichment delegates. |
| `IncludeStatusCode` | `bool` | `true` | Records HTTP status code tag (e.g. `"http_status": "200"`). |
| `IncludeHttpMethod` | `bool` | `true` | Records HTTP method tag (e.g. `"http_method": "GET"`). |
| `IncludeExceptionType` | `bool` | `true` | Records exception type name if an unhandled exception occurs. |
| `TrackFailedStatusCodes` | `bool` | `true` | Marks operations as failed if HTTP status is &gt;= 400. |

### 2. Primary Extension Methods

```csharp
// Registers ASP.NET Core integration in IServiceCollection
services.AddMetricFlow("MyWebTopic", options => { ... });

// Adds the request tracking middleware to IApplicationBuilder
app.UseMetricFlow();

// Exposes formatted metrics endpoint in IEndpointRouteBuilder
app.MapMetricFlow("/metrics");
```

---

## Frequently Asked Questions (FAQ)

### Q: How do I capture HTTP request headers as metric tags?

**A:** Use `AddHttpTagsEnricher` to inspect the `HttpContext` and populate tags:

```csharp
builder.Services.AddMetricFlow("WebApi", options =>
{
    options.AddHttpTagsEnricher((tags, context) =>
    {
        if (context.Request.Headers.TryGetValue("X-Client-ID", out var clientId))
        {
            tags["client_id"] = clientId!;
        }

        if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
        {
            tags["user_agent"] = userAgent!;
        }
    });
});
```

---

### Q: How do I exclude Swagger, health checks, or static files from metric tracking?

**A:** Add exact paths or path prefixes to `ExcludedPaths` and `ExcludePathPrefixes`:

```csharp
builder.Services.AddMetricFlow("WebApi", options =>
{
    // Ignore all Swagger documentation paths
    options.ExcludePathPrefixes.Add("/swagger");

    // Dynamic predicate exclusion (e.g. skip static file extensions)
    options.ExcludePredicate = context =>
        context.Request.Path.Value?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true;
});
```

---

### Q: How do I customize route naming or group parameterized URLs?

**A:** By default, `NamingStrategy = MetricRouteNamingStrategy.RoutePattern` automatically matches route templates like `/users/{id}` instead of raw URLs (`/users/123`), preventing high-cardinality explosions. For custom logic, provide a `RoutePatternResolver`:

```csharp
options.RoutePatternResolver = context =>
{
    var endpoint = context.GetEndpoint();
    if (endpoint != null && !string.IsNullOrEmpty(endpoint.DisplayName))
    {
        return endpoint.DisplayName;
    }

    return null; // Fallback to default resolver
};
```

---

### Q: How do I inject `IMetricTracker` into Minimal API endpoints or MVC controllers?

**A:** Simply declare `IMetricTracker` as a parameter in your endpoint delegate or controller constructor:

```csharp
// Minimal API injection
app.MapPost("/orders", async (OrderDto order, IMetricTracker tracker) =>
{
    using (tracker.Track("ProcessOrderValidation"))
    {
        await ValidateOrderAsync(order);
    }

    return Results.Created($"/orders/{order.Id}", order);
});

// MVC Controller injection
public class PaymentsController(IMetricTracker tracker) : ControllerBase
{
    [HttpPost("charge")]
    public async Task<IActionResult> Charge()
    {
        using var scope = tracker.Track("StripeChargeCall");
        await Task.Delay(20);
        return Ok();
    }
}
```

---

### Q: How do I secure or restrict access to the `/metrics` endpoint?

**A:** Apply authorization policies or host filters directly onto the `MapMetricFlow` route registration:

```csharp
// Require authorization policy
app.MapMetricFlow("/metrics")
   .RequireAuthorization("AdminOnlyPolicy");

// Or restrict to internal network / management port
app.MapMetricFlow("/metrics")
   .RequireHost("internal.company.local");
```
