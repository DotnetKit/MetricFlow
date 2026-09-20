using System.Collections.Concurrent;
using System.Text;
using DotnetKit.MetricFlow.Abstractions;

namespace DotnetKit.MetricFlow.Counters;

/// <summary>
/// Metric counter that aggregates operation counts categorized by a user-specified tag or metadata dimension,
/// with built-in cardinality safeguards against memory leaks.
/// </summary>
public class TagBreakdownCounter : CounterBase<object?>
{
    private readonly ConcurrentDictionary<string, MetricTagBreakdownState> _states = new(StringComparer.OrdinalIgnoreCase);

    public string TagKey { get; }
    public int MaxUniqueValues { get; }
    public string OverflowBucket { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TagBreakdownCounter"/>.
    /// </summary>
    /// <param name="tagKey">The target tag or metadata key to aggregate on (e.g. "country", "status", "category").</param>
    /// <param name="name">Optional custom counter name. Defaults to "TagBreakdown:{tagKey}".</param>
    /// <param name="maxUniqueValues">Maximum number of unique tag values tracked before overflow rollup. Defaults to 250.</param>
    /// <param name="overflowBucket">The bucket name for distinct values exceeding <paramref name="maxUniqueValues"/>. Defaults to "[Other]".</param>
    public TagBreakdownCounter(
        string tagKey,
        string? name = null,
        int maxUniqueValues = 250,
        string overflowBucket = "[Other]")
        : base(name ?? $"TagBreakdown:{tagKey}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagKey);
        if (maxUniqueValues <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUniqueValues), "MaxUniqueValues must be greater than zero.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(overflowBucket);

        TagKey = tagKey;
        MaxUniqueValues = maxUniqueValues;
        OverflowBucket = overflowBucket;
    }

    public override object? OnIn(in InContext context) => null;

    public override void OnOut(object? state, in OutContext context)
    {
        if (!IsEnabled) return;

        var breakdownState = _states.GetOrAdd(
            context.MetricName,
            static (name, arg) => new MetricTagBreakdownState(name, arg.TagKey, arg.MaxUniqueValues, arg.OverflowBucket),
            (TagKey, MaxUniqueValues, OverflowBucket));

        string? tagValue = null;
        if (context.Tags != null)
        {
            if (context.Tags.TryGetValue(TagKey, out var directVal))
            {
                tagValue = directVal;
            }
            else
            {
                foreach (var (k, v) in context.Tags)
                {
                    if (string.Equals(k, TagKey, StringComparison.OrdinalIgnoreCase))
                    {
                        tagValue = v;
                        break;
                    }
                }
            }
        }

        if (tagValue == null && context.Metadata != null)
        {
            if (context.Metadata.TryGetValue(TagKey, out var directMeta))
            {
                tagValue = directMeta.ToString();
            }
            else
            {
                foreach (var (k, v) in context.Metadata)
                {
                    if (string.Equals(k, TagKey, StringComparison.OrdinalIgnoreCase))
                    {
                        tagValue = v.ToString();
                        break;
                    }
                }
            }
        }

        breakdownState.Record(tagValue, context.Failed);
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

    public MetricTagBreakdownState? GetState(string metricName)
    {
        _states.TryGetValue(metricName, out var state);
        return state;
    }

    public class MetricTagBreakdownState(string metricName, string tagKey, int maxUniqueValues, string overflowBucket)
    {
        private long _totalOperations;
        private long _taggedOperations;
        private long _untaggedOperations;
        private long _failedOperations;
        private readonly ConcurrentDictionary<string, long> _breakdown = new(StringComparer.OrdinalIgnoreCase);

        public string MetricName => metricName;
        public string TagKey => tagKey;
        public int MaxUniqueValues => maxUniqueValues;
        public string OverflowBucket => overflowBucket;

        public long TotalOperations => Interlocked.Read(ref _totalOperations);
        public long TaggedOperations => Interlocked.Read(ref _taggedOperations);
        public long UntaggedOperations => Interlocked.Read(ref _untaggedOperations);
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

        public TagBreakdownSnapshot ToSnapshot(string counterName)
        {
            return new TagBreakdownSnapshot(
                MetricName: metricName,
                CounterName: counterName,
                TagKey: tagKey,
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

public record TagBreakdownSnapshot(
    string MetricName,
    string CounterName,
    string TagKey,
    long TotalOperations,
    long TaggedOperations,
    long UntaggedOperations,
    long FailedOperations,
    IReadOnlyDictionary<string, long> Breakdown,
    DateTime Timestamp) : IMetricSnapshot
{
    public double TaggedPercentage => TotalOperations > 0 ? (double)TaggedOperations / TotalOperations : 0.0;

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
            sb.AppendLine($"Breakdown by '{TagKey}':");
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
