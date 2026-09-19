namespace DotnetKit.MetricFlow.Abstractions.Sinks;

/// <summary>
/// Identifies the service instance, host, and cloud/container environment for telemetry correlation.
/// </summary>
public sealed record ResourceMetadata(
    string ServiceName,
    string InstanceId,
    string? PodName = null,
    string? EnvironmentName = null)
{
    /// <summary>
    /// Detects runtime instance metadata from common environment variables (Kubernetes, ECS, Azure, etc.).
    /// </summary>
    public static ResourceMetadata Detect(string? serviceName = null)
    {
        var resolvedServiceName = serviceName
            ?? Environment.GetEnvironmentVariable("DOTNETKIT_SERVICE_NAME")
            ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name
            ?? "MetricFlowService";

        var podName = Environment.GetEnvironmentVariable("POD_NAME")
            ?? Environment.GetEnvironmentVariable("HOSTNAME");

        var instanceId = Environment.GetEnvironmentVariable("DOTNETKIT_INSTANCE_ID")
            ?? Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")
            ?? podName
            ?? Environment.MachineName;

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        return new ResourceMetadata(
            ServiceName: resolvedServiceName,
            InstanceId: instanceId,
            PodName: podName,
            EnvironmentName: env
        );
    }
}
