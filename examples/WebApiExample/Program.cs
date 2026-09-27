using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MetricFlow with custom tag enrichment and additional topic tracker via fluent builder
builder.Services.AddMetricFlow("WebApiExample", options =>
{
    options.AddHttpTagsEnricher((tags, context) =>
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            tags["tenant_id"] = tenantId!;
        }
    });
})
.AddMetricTracker("WeatherRadar");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Turnkey MetricFlow tracking middleware
app.UseMetricFlow();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (IMetricTracker tracker, string? country = null) =>
{
    using var scope = !string.IsNullOrWhiteSpace(country)
        ? tracker.Track("QueryedByCountryWheather", new Dictionary<string, string> { { "country", country } })
        : null;

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)],
            country
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/throw_exception", () =>
{
    throw new Exception("This is an exception");
})
.WithName("Exception")
.WithOpenApi();

app.MapGet("/radar/scan", (IMetricFlow metricFlow) =>
{
    var tracker = metricFlow.GetTracker("WeatherRadar");
    using var operation = tracker.Track("ScanRadar");
    return Results.Ok(new { message = "Radar scan tracked", topic = tracker.Topic });
})
.WithName("ScanRadar")
.WithOpenApi();

// Expose metrics endpoint
app.MapMetricFlow()
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary, string? Country = null)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
