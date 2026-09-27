namespace DotnetKit.MetricFlow;

/// <summary>
/// Metadata representing a registered metric tracker topic in the dependency injection container.
/// </summary>
public sealed record MetricTrackerRegistration(string Topic);
