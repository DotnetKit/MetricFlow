using System.Collections.Concurrent;

namespace DotnetKit.MetricFlow.Meters;

/// <summary>
/// Thread-safe cardinality protector that tracks unique values per tag key
/// and clamps excess distinct values to an overflow bucket before emission to System.Diagnostics.Metrics.
/// </summary>
public class TagCardinalityGuard
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _seenTagValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly MetricFlowMeterOptions _options;

    public TagCardinalityGuard(MetricFlowMeterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Sanitizes the tag value according to configured cardinality limits.
    /// If the tag key has reached its limit and the value has not been previously observed,
    /// the overflow bucket value (e.g. "[Other]") is returned.
    /// </summary>
    public string SanitizeTagValue(string key, string value)
    {
        if (!_options.EnforceCardinalityLimitsOnMeters)
        {
            return value;
        }

        if (string.Equals(value, _options.OverflowBucket, StringComparison.OrdinalIgnoreCase))
        {
            return _options.OverflowBucket;
        }

        int maxLimit = _options.TagCardinalityLimits.TryGetValue(key, out var customLimit)
            ? customLimit
            : _options.MaxUniqueTagValues;

        var seenSet = _seenTagValues.GetOrAdd(key, static _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        if (seenSet.ContainsKey(value))
        {
            return value;
        }

        if (seenSet.Count >= maxLimit)
        {
            return _options.OverflowBucket;
        }

        if (seenSet.TryAdd(value, 0))
        {
            if (seenSet.Count > maxLimit)
            {
                seenSet.TryRemove(value, out _);
                return _options.OverflowBucket;
            }

            return value;
        }

        return value;
    }

    /// <summary>
    /// Gets the count of observed unique values for a specific tag key.
    /// </summary>
    public int GetUniqueCount(string key)
    {
        return _seenTagValues.TryGetValue(key, out var seenSet) ? seenSet.Count : 0;
    }

    /// <summary>
    /// Resets all tracked cardinality sets.
    /// </summary>
    public void Reset()
    {
        _seenTagValues.Clear();
    }
}
