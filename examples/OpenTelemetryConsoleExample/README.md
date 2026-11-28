# OpenTelemetry Console Example

This example demonstrates native **OpenTelemetry** integration with **MetricFlow** using the `DotnetKit.MetricFlow.OpenTelemetry` package. It showcases how MetricFlow operations automatically bridge into standard .NET `System.Diagnostics.Metrics` (`Meter`) instruments while retaining MetricFlow's in-memory aggregated snapshots and cardinality guards.

<img width="1319" height="1011" alt="image" src="https://github.com/user-attachments/assets/0c78d11f-f7c0-4f8d-a94b-40a42bd5acd7" />

---

## Highlights

- **Native OpenTelemetry Bridge**: Automatically translates MetricFlow operations into official BCL `System.Diagnostics.Metrics` instruments:
  - `<topic>.duration` (`Histogram<double>`, milliseconds)
  - `<topic>.operations` (`Counter<long>`)
  - `<topic>.items` (`Counter<long>`)
  - `<topic>.active_operations` (`UpDownCounter<long>`)
  - `<topic>.exceptions` (`Counter<long>`)
- **Unified DI Registration (`.WithOpenTelemetry`)**: Configures MetricFlow and the OpenTelemetry `MeterProvider` in a single fluent builder call.
- **Tag Cardinality Guard**: Protects Prometheus, Datadog, and OTLP backends from metric explosion:
  - Caps maximum unique tag values per key (e.g., `MaxUniqueTagValues = 3`).
  - Clamps overflowing values into a configurable fallback bucket (`[Other]`).
- **Dual Telemetry Pipeline**:
  - **OpenTelemetry Export**: Streams raw BCL metric events to standard exporters (Console, OTLP, Prometheus).
  - **Local MetricFlow Snapshots**: Computes in-memory statistical summaries (`min`, `max`, `avg`, throughput rate).
- **Compatible with `dotnet-counters`**: Metrics can be observed live via CLI tooling without any external collectors.

---

## Code Walkthrough

### 1. DI Configuration with `.WithOpenTelemetry()`

```csharp
var services = new ServiceCollection();

services.AddMetricFlow("OrderProcessingService", options =>
{
    options.AddTagsEnricher(tags =>
    {
        tags["environment"] = "Production";
        tags["datacenter"] = "eu-central-1";
    });

    options.AddTelemetryCounters()
           .AddThroughputCounter();

    // Guard against metric explosion on high-cardinality tags
    options.ConfigureMeters(m =>
    {
        m.MaxUniqueTagValues = 3;
        m.OverflowBucket = "[Other]";
    });
})
.WithOpenTelemetry(otel =>
{
    otel.WithMetrics(metrics =>
    {
        metrics.AddConsoleExporter((_, readerOptions) =>
        {
            readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
        });

        // Exports via OTLP using standard OTEL_EXPORTER_OTLP_* environment variables
        metrics.AddOtlpExporter();
    });
});

using var serviceProvider = services.BuildServiceProvider();
var tracker = serviceProvider.GetRequiredService<IMetricTracker>();
var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();
```

### 2. Operational Workflows

```csharp
// 1. Batch throughput with regional dimensions
using (var scope = tracker.Track("ProcessOrderBatch", new() { ["region"] = "EU" }))
{
    scope.SetItems(150);
    await Task.Delay(15);
}

// 2. High-cardinality tag clamping
// Customer IDs beyond the first 3 will be clamped to "[Other]"
for (int i = 1; i <= 8; i++)
{
    using (tracker.Track("CustomerLookup", new() { ["customer_id"] = $"cust_{i:000}" }))
    {
        await Task.Delay(5);
    }
}

// 3. Exception tracking
try
{
    using (tracker.Track("ValidatePayment"))
    {
        throw new InvalidOperationException("Gateway timeout.");
    }
}
catch (Exception ex)
{
    tracker.Out("ValidatePayment", failed: true, exception: ex);
}
```

### 3. Dual Telemetry Flush & Report

```csharp
// 1. Flush OpenTelemetry raw measurements
meterProvider.ForceFlush();

// 2. Output MetricFlow aggregated snapshots
Console.WriteLine(tracker.ToString());
```

---

## Quickstart

### 1. Start the Local Observability Stack (OTel Collector + Prometheus + Grafana)

From the repository root:

```bash
docker compose -f examples/OpenTelemetryConsoleExample/docker-compose.yml up -d
```

This starts:
- **OpenTelemetry Collector**: `http://localhost:4318` (OTLP HTTP/protobuf) and `localhost:4317` (OTLP gRPC)
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (anonymous admin login enabled, pre-provisioned **MetricFlow - OpenTelemetry Console Example** home dashboard)

### 2. Run the Example

Pass the OTLP environment variables inline (use `--no-launch-profile` if you also have a local `Properties/launchSettings.json`):

```bash
OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4318" \
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf" \
OTEL_SERVICE_NAME="metricflow-otel-example" \
dotnet run --no-launch-profile --project examples/OpenTelemetryConsoleExample/OpenTelemetryConsoleExample.csproj
```

> **IDE tip (`Properties/launchSettings.json`):** `**/launchSettings.json` is gitignored so you can keep local or authenticated endpoints out of source control:
> ```json
> {
>   "profiles": {
>     "LocalDocker": {
>       "commandName": "Project",
>       "environmentVariables": {
>         "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4318",
>         "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
>         "OTEL_SERVICE_NAME": "metricflow-otel-example"
>       }
>     }
>   }
> }
> ```

### 3. Inspect Metrics

- **Grafana:** Open http://localhost:3000 — the preconfigured dashboard displays order batches, item totals by region, customer lookup cardinality clamping (`[Other]`), payment exceptions, and average operation durations.
- **Prometheus:** Open http://localhost:9090 and query `last_over_time({service_name="metricflow-otel-example"}[1h])` or `ProcessOrderBatch_total`.

### 4. Stop & Clean Up

```bash
docker compose -f examples/OpenTelemetryConsoleExample/docker-compose.yml down -v
```

### Custom / Remote OTLP Endpoint

```bash
OTEL_EXPORTER_OTLP_ENDPOINT="https://your-otel-collector.example.com" \
OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf" \
OTEL_SERVICE_NAME="metricflow-otel-example" \
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer <YOUR_TOKEN>" \
dotnet run --no-launch-profile --project examples/OpenTelemetryConsoleExample/OpenTelemetryConsoleExample.csproj
```

### Observing Live with `dotnet-counters`

While the application is running (or in your own production service), you can inspect metrics live via the .NET CLI:

```bash
dotnet-counters monitor --counters OrderProcessingService
```
