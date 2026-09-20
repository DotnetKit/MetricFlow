using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Metric counter that aggregates operation counts categorized by a user-specified tag,
/// multi-tag combination, or custom computed dimension selector lambda,
/// with built-in cardinality safeguards against memory leaks.
/// </summary>
public class DimensionCounter : CounterBase<object?>
{
    private readonly ConcurrentDictionary<string, MetricDimensionState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> _dimensionSelector;

    public string DimensionName { get; }
    public string TagKey => DimensionName;
    public int MaxUniqueValues { get; }
    public string OverflowBucket { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DimensionCounter"/> targeting a single tag or metadata key.
    /// </summary>
    /// <param name="dimensionKey">The target tag or metadata key to aggregate on (e.g. "country", "status", "category").</param>
    /// <param name="name">Optional custom counter name. Defaults to "Dimension:{dimensionKey}".</param>
    /// <param name="maxUniqueValues">Maximum number of unique tag values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    public DimensionCounter(
        string dimensionKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        : this(
            name: name ?? $"Dimension:{dimensionKey}",
            selector: (tags, meta) => ExtractSingleTag(tags, meta, dimensionKey),
            dimensionName: dimensionKey,
            maxUniqueValues: maxUniqueValues,
            overflowBucket: overflowBucket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionKey);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DimensionCounter"/> targeting a combination of multiple tags.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="dimensionKeys">The list of tag keys to combine (e.g. ["country", "payment_method"]).</param>
    /// <param name="delimiter">Delimiter used to join tag values. Defaults to " / ".</param>
    /// <param name="maxUniqueValues">Maximum number of unique tag combinations tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct combinations exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    public DimensionCounter(
        string name,
        IEnumerable<string> dimensionKeys,
        string delimiter = " / ",
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        : this(
            name: name,
            selector: (tags, meta) => ExtractMultiTags(tags, meta, dimensionKeys.ToArray(), delimiter),
            dimensionName: string.Join(delimiter, dimensionKeys),
            maxUniqueValues: maxUniqueValues,
            overflowBucket: overflowBucket)
    {
        ArgumentNullException.ThrowIfNull(dimensionKeys);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DimensionCounter"/> with a custom computed key selector lambda.
    /// Enables conditional business counters, classification rules, or dynamic multi-attribute aggregations.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="selector">Function computing the dimension key from tags and metadata. Return null to skip or mark untagged.</param>
    /// <param name="maxUniqueValues">Maximum number of unique values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    /// <param name="dimensionName">Optional descriptive dimension label. Defaults to <paramref name="name"/>.</param>
    public DimensionCounter(
        string name,
        Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> selector,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]",
        string? dimensionName = null)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selector);
        if (maxUniqueValues <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUniqueValues), "MaxUniqueValues must be greater than zero.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(overflowBucket);

        _dimensionSelector = selector;
        DimensionName = dimensionName ?? name;
        MaxUniqueValues = maxUniqueValues;
        OverflowBucket = overflowBucket;
    }

    public override object? OnIn(in InContext context) => null;

    public override void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        var breakdownState = _states.GetOrAdd(
            context.MetricName,
            static (name, arg) => new MetricDimensionState(name, arg.DimensionName, arg.MaxUniqueValues, arg.OverflowBucket),
            (DimensionName, MaxUniqueValues, OverflowBucket));

        string? dimensionValue;
        try
        {
            dimensionValue = _dimensionSelector(context.Tags, context.Metadata);
        }
        catch
        {
            // Protect against unexpected exceptions in custom user selector lambdas
            dimensionValue = null;
        }

        breakdownState.Record(dimensionValue, context.Failed);
    }

    public override IMetricSnapshot? GetSnapshot(string metricName)
    {
        return _states.TryGetValue(metricName, out var state) ? state.ToSnapshot(Name) : null;
    }

    public override IEnumerable<IMetricSnapshot> GetAllSnapshots()
    {
        foreach (var state in _states.Values)
        {
            yield return state.ToSnapshot(Name);
        }
    }

    public override void Reset() => _states.Clear();

    public MetricDimensionState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    private static string? ExtractSingleTag(
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, long>? metadata,
        string key)
    {
        if (tags != null)
        {
            if (tags.TryGetValue(key, out var directVal))
            {
                return directVal;
            }

            foreach (var (k, v) in tags)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }
        }

        if (metadata != null)
        {
            if (metadata.TryGetValue(key, out var directMeta))
            {
                return directMeta.ToString();
            }

            foreach (var (k, v) in metadata)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    return v.ToString();
                }
            }
        }

        return null;
    }

    private static string? ExtractMultiTags(
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, long>? metadata,
        IReadOnlyList<string> keys,
        string delimiter)
    {
        if (keys.Count == 0) return null;

        var values = new string[keys.Count];
        bool anyPresent = false;

        for (int i = 0; i < keys.Count; i++)
        {
            var val = ExtractSingleTag(tags, metadata, keys[i]);
            if (!string.IsNullOrWhiteSpace(val))
            {
                values[i] = val;
                anyPresent = true;
            }
            else
            {
                values[i] = "-";
            }
        }

        return anyPresent ? string.Join(delimiter, values) : null;
    }

    public class MetricDimensionState(string metricName, string dimensionName, int maxUniqueValues, string overflowBucket)
    {
        private long _totalOperations;
        private long _taggedOperations;
        private long _untaggedOperations;
        private long _failedOperations;
        private readonly ConcurrentDictionary<string, long> _breakdown = new(StringComparer.OrdinalIgnoreCase);

        public string MetricName => metricName;
        public string DimensionName => dimensionName;
        public string TagKey => dimensionName;
        public int MaxUniqueValues => maxUniqueValues;
        public string OverflowBucket => overflowBucket;

        public long TotalOperations => Interlocked.Read(ref _totalOperations);
        public long TaggedOperations => Interlocked.Read(ref _taggedOperations);
        public long TrackedOperations => TaggedOperations;
        public long UntaggedOperations => Interlocked.Read(ref _untaggedOperations);
        public long UntrackedOperations => UntaggedOperations;
        public long FailedOperations => Interlocked.Read(ref _failedOperations);

        public IReadOnlyDictionary<string, long> Breakdown => new Dictionary<string, long>(_breakdown, StringComparer.OrdinalIgnoreCase);

        public void Record(string? tagValue, bool failed = false)
        {
            Interlocked.Increment(ref _totalOperations);
            if (failed)
            {
                Interlocked.Increment(ref _failedOperations);
            }

            if (string.IsNullOrWhiteSpace(tagValue))
            {
                Interlocked.Increment(ref _untaggedOperations);
                return;
            }

            Interlocked.Increment(ref _taggedOperations);

            // Fast path: value is already known
            if (_breakdown.ContainsKey(tagValue))
            {
                _breakdown.AddOrUpdate(tagValue, 1, static (_, count) => count + 1);
                return;
            }

            // Cardinality check before inserting a new distinct key
            if (_breakdown.Count >= maxUniqueValues)
            {
                _breakdown.AddOrUpdate(overflowBucket, 1, static (_, count) => count + 1);
            }
            else
            {
                _breakdown.AddOrUpdate(tagValue, 1, static (_, count) => count + 1);
            }
        }

        public DimensionSnapshot ToSnapshot(string counterName)
        {
            return new DimensionSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                DimensionName: dimensionName,
                TotalOperations: TotalOperations,
                TaggedOperations: TaggedOperations,
                UntaggedOperations: UntaggedOperations,
                FailedOperations: FailedOperations,
                Breakdown: Breakdown,
                Timestamp: DateTime.UtcNow
            );
        }
    }
}

/// <summary>
/// Alias for <see cref="DimensionCounter"/> with tag-oriented naming.
/// </summary>
public class TagBreakdownCounter : DimensionCounter
{
    public TagBreakdownCounter(
        string tagKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        : base(tagKey, name ?? $"TagBreakdown:{tagKey}", maxUniqueValues, overflowBucket)
    {
    }

    public TagBreakdownCounter(
        string name,
        IEnumerable<string> tagKeys,
        string delimiter = " / ",
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        : base(name, tagKeys, delimiter, maxUniqueValues, overflowBucket)
    {
    }

    public TagBreakdownCounter(
        string name,
        Func<IReadOnlyDictionary<string, string>?, IReadOnlyDictionary<string, long>?, string?> selector,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]",
        string? dimensionName = null)
        : base(name, selector, maxUniqueValues, overflowBucket, dimensionName)
    {
    }
}

public record DimensionSnapshot(
    string MetricName,
    string CounterName,
    string DimensionName,
    long TotalOperations,
    long TaggedOperations,
    long UntaggedOperations,
    long FailedOperations,
    IReadOnlyDictionary<string, long> Breakdown,
    DateTime Timestamp) : IMetricSnapshot
{
    /// <summary>
    /// Alias for <see cref="DimensionName"/> for backward compatibility.
    /// </summary>
    public string TagKey => DimensionName;

    public long TrackedOperations => TaggedOperations;
    public long UntrackedOperations => UntaggedOperations;
    public double TaggedPercentage => TotalOperations > 0 ? (double)TaggedOperations / TotalOperations : 0.0;
    public double TrackedPercentage => TaggedPercentage;

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{CounterName}] Metric: {MetricName}");
        sb.AppendLine($"Total Operations       : {TotalOperations:N0}");
        sb.AppendLine($"Tagged Operations      : {TaggedOperations:N0} ({TaggedPercentage * 100:F1}%)");
        if (UntaggedOperations > 0)
        {
            sb.AppendLine($"Untagged Operations    : {UntaggedOperations:N0}");
        }
        if (FailedOperations > 0)
        {
            sb.AppendLine($"Failed Operations      : {FailedOperations:N0}");
        }

        if (Breakdown.Count > 0)
        {
            sb.AppendLine($"Breakdown by '{DimensionName}':");
            foreach (var (key, count) in Breakdown.OrderByDescending(kv => kv.Value))
            {
                double pct = TaggedOperations > 0 ? (double)count / TaggedOperations * 100.0 : 0.0;
                sb.AppendLine($"  - {key}: {count:N0} ({pct:F1}%)");
            }
        }

        return sb.ToString();
    }

    public override string ToString() => ToFormattedString();
}

/// <summary>
/// Alias for <see cref="DimensionSnapshot"/>.
/// </summary>
public record TagBreakdownSnapshot(
    string MetricName,
    string CounterName,
    string DimensionName,
    long TotalOperations,
    long TaggedOperations,
    long UntaggedOperations,
    long FailedOperations,
    IReadOnlyDictionary<string, long> Breakdown,
    DateTime Timestamp) : DimensionSnapshot(MetricName, CounterName, DimensionName, TotalOperations, TaggedOperations, UntaggedOperations, FailedOperations, Breakdown, Timestamp);
