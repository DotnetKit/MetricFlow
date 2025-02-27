using DotnetKit.MetricFlow.Tracker;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(sp => new MetricTracker("WebApiExample"));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    //add a middleware to track API calls
    app.Use(async (context, next) =>
    {
        //skip tracking metrics for the metrics endpoint
        if (context.Request.Path == "/metrics")
        {
            await next();
            return;
        }
        var tracker = context.RequestServices.GetRequiredService<MetricTracker>();
        tracker.In(context.Request.Path);
        //tracking requests with errors
        try
        {
            await next();
        }
        catch (Exception e)
        {
            tracker.Out(context.Request.Path, new Dictionary<string, string>() { ["error_message"] = e.Message }, failed: true);
            throw;
        }
        tracker.Out(context.Request.Path);
    });
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
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

app.MapGet("/metrics", () =>
{
    var tracker = app.Services.GetRequiredService<MetricTracker>();
    return tracker.ToString();

})
.WithName("Metrics")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
