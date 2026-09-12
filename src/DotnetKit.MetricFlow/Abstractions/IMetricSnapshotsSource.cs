namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Defines a source that can provide all metric snapshots.
/// </summary>
public interface IMetricSnapshotsSource
{
    /// <summary>
    /// Gets all metric snapshots collected by this source.
    /// </summary>
    /// <returns>An enumerable collection of metric snapshots.</returns>
    IEnumerable<IMetricSnapshot> GetAllSnapshots();
}
