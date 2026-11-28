using DotnetKit.MetricFlow.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace OpenTelemetryConsoleExample;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProcessingMetrics(this IServiceCollection services)
    {
        services
            .AddMetricFlow(
                "OrderProcessingService",
                options =>
                {
                    options.AddTagsEnricher(tags =>
                    {
                        tags["environment"] = "Production";
                        tags["datacenter"] = "eu-central-1";
                    });

                    // Add telemetry preset (duration + exceptions), throughput counter, and pure functional metrics
                    options
                        .AddTelemetryCounters()
                        .AddThroughputCounter()
                        .AddFunctionalMetrics("region", "plan");

                    // Configure meter cardinality protection to protect Prometheus / OTLP collectors
                    options.ConfigureMeters(m =>
                    {
                        m.MaxUniqueTagValues = 3;
                        m.OverflowBucket = "[Other]";
                    });
                }
            )
            .WithOpenTelemetry(otel =>
            {
                otel.WithMetrics(metrics =>
                {
                    // Export directly to console (in production, use .AddOtlpExporter() or .AddPrometheusExporter())
                    metrics.AddConsoleExporter(
                        (_, metricReaderOptions) =>
                        {
                            metricReaderOptions
                                .PeriodicExportingMetricReaderOptions
                                .ExportIntervalMilliseconds = 1000;
                        }
                    );

                    // Export via OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is configured
                    var otlpEndpoint = Environment.GetEnvironmentVariable(
                        "OTEL_EXPORTER_OTLP_ENDPOINT"
                    );
                    if (string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        throw new InvalidOperationException(
                            "OTEL_EXPORTER_OTLP_ENDPOINT is not configured."
                        );
                    }

                    metrics.AddOtlpExporter(otlp =>
                    {
                        if (!otlpEndpoint.Contains("://", StringComparison.Ordinal))
                        {
                            otlp.Endpoint = new Uri(
                                $"https://{otlpEndpoint.TrimEnd('/')}/v1/metrics"
                            );
                        }

                        Console.WriteLine(
                            $"[OTLP] Endpoint={otlp.Endpoint}, Protocol={otlp.Protocol}, HeadersConfigured={!string.IsNullOrWhiteSpace(otlp.Headers)}"
                        );
                    });
                });
            });

        return services;
    }
}
