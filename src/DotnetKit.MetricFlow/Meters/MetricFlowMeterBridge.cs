using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace DotnetKit.MetricFlow.Meters;

/// <summary>
/// Bridge implementation connecting MetricFlow operations to System.Diagnostics.Metrics instruments.
/// </summary>
public class MetricFlowMeterBridge : IMetricMeterBridge
{
    private readonly string _topic;
    private readonly Dictionary<string, string>? _topicTags;
    private readonly MetricFlowMeterOptions _options;
    private readonly Meter _meter;
    private readonly TagCardinalityGuard _cardinalityGuard;
    private readonly ConcurrentDictionary<string, OperationInstruments> _instruments = new(StringComparer.OrdinalIgnoreCase);

    private int _disposed;

    public string Topic => _topic;
    public Meter Meter => _meter;
    public bool IsEnabled => _options.Enabled && _disposed == 0;
    public MetricFlowMeterOptions Options => _options;

    public MetricFlowMeterBridge(
        string topic,
        Dictionary<string, string>? topicTags = null,
        MetricFlowMeterOptions? options = null)
    {
        _topic = string.IsNullOrWhiteSpace(topic) ? "Application" : topic;
        _topicTags = topicTags != null ? new Dictionary<string, string>(topicTags, StringComparer.OrdinalIgnoreCase) : null;
        _options = options ?? new MetricFlowMeterOptions();
        _cardinalityGuard = new TagCardinalityGuard(_options);

        var meterName = BuildMeterName(_options.MeterPrefix, _topic);
        var version = typeof(MetricFlowMeterBridge).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        _meter = new Meter(meterName, version);
    }

    public TagList? RecordOperationIn(
        string metricName,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, long>? metadata)
    {
        if (!IsEnabled || !_options.RecordActiveOperations)
        {
            return null;
        }

        var instruments = GetOrAddInstruments(metricName);
        var tagList = CreateActiveTagList(metricName, tags);
        instruments.Active.Add(1, in tagList);
        return tagList;
    }
    #pragma warning disable RCS1242 // Retains zero-copy reference passing directly into BCL's UpDownCounter.Add(..., in ...)
    public void RecordOperationInFlightEnd(string metricName, in TagList inFlightTags)
    {
        if (!IsEnabled || !_options.RecordActiveOperations)
        {
            return;
        }

        var instruments = GetOrAddInstruments(metricName);
        instruments.Active.Add(-1, in inFlightTags);
    }

    public void RecordOperationOut(
        string metricName,
        TimeSpan duration,
        bool failed,
        Exception? exception,
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, long>? metadata)
    {
        if (!IsEnabled)
        {
            return;
        }

        var instruments = GetOrAddInstruments(metricName);

        // 1. Duration histogram: {metricName}.duration (ms)
        var status = failed || exception != null ? "error" : "ok";
        var durationTags = CreateMeasurementTagList(metricName, status: status, exceptionType: null, tags);
        instruments.Duration.Record(duration.TotalMilliseconds, in durationTags);

        // 2. Execution counts: {metricName}.total
        instruments.Total.Add(1, in durationTags);

        // 3. Throughput / Items: {metricName}.items
        if (ItemCountExtractor.TryExtractItemCount(tags, metadata, out var items))
        {
            var itemTags = CreateMeasurementTagList(metricName, status: null, exceptionType: null, tags);
            instruments.Items.Add(items, in itemTags);
        }
        else if (_options.AlwaysRecordItems)
        {
            var itemTags = CreateMeasurementTagList(metricName, status: null, exceptionType: null, tags);
            instruments.Items.Add(1, in itemTags);
        }

        // 4. ExceptionCounter: {metricName}.exceptions
        if (failed || exception != null)
        {
            var exType = exception?.GetType().FullName ?? "OperationFailed";
            var exTags = CreateMeasurementTagList(metricName, status: "error", exceptionType: exType, tags);
            instruments.Exceptions.Add(1, in exTags);
        }
    }

    public TagList CreateActiveTagList(string metricName, IReadOnlyDictionary<string, string>? tags)
    {
        var tagList = new TagList { { "operation", metricName } };
        AppendSanitizedTags(ref tagList, tags);
        return tagList;
    }

    public TagList CreateMeasurementTagList(
        string metricName,
        string? status,
        string? exceptionType,
        IReadOnlyDictionary<string, string>? tags)
    {
        var tagList = new TagList { { "operation", metricName } };

        if (status != null)
        {
            tagList.Add("status", status);
        }

        if (exceptionType != null)
        {
            tagList.Add("exception.type", exceptionType);
        }

        AppendSanitizedTags(ref tagList, tags);
        return tagList;
    }

    private void AppendSanitizedTags(ref TagList tagList, IReadOnlyDictionary<string, string>? tags)
    {
        // Add topic tags if enabled
        if (_options.IncludeTopicTags && _topicTags != null)
        {
            foreach (var (k, v) in _topicTags)
            {
                // If scope tags contain the same key, let scope tags override
                if (tags != null && tags.ContainsKey(k))
                {
                    continue;
                }

                var sanitized = _cardinalityGuard.SanitizeTagValue(k, v);
                tagList.Add(k, sanitized);
            }
        }

        // Add scope tags
        if (tags != null)
        {
            foreach (var (k, v) in tags)
            {
                var sanitized = _cardinalityGuard.SanitizeTagValue(k, v);
                tagList.Add(k, sanitized);
            }
        }
    }

    private OperationInstruments GetOrAddInstruments(string metricName)
    {
        var lookupKey = _options.NamingConvention == MetricInstrumentNamingConvention.SharedOperation
            ? "__shared__"
            : metricName;

        return _instruments.GetOrAdd(lookupKey, _ =>
        {
            var prefix = _options.NamingConvention == MetricInstrumentNamingConvention.SharedOperation
                ? "operation"
                : SanitizeInstrumentMetricName(metricName);

            return new OperationInstruments
            {
                Duration = _meter.CreateHistogram<double>(
                    name: $"{prefix}.duration",
                    unit: "ms",
                    description: "Duration of operation in milliseconds."),
                Total = _meter.CreateCounter<long>(
                    name: $"{prefix}.total",
                    unit: "{operations}",
                    description: "Total number of operations executed."),
                Items = _meter.CreateCounter<long>(
                    name: $"{prefix}.items",
                    unit: "{items}",
                    description: "Total number of items processed."),
                Exceptions = _meter.CreateCounter<long>(
                    name: $"{prefix}.exceptions",
                    unit: "{exceptions}",
                    description: "Total number of exceptions encountered."),
                Active = _meter.CreateUpDownCounter<long>(
                    name: $"{prefix}.active",
                    unit: "{operations}",
                    description: "Number of currently active in-flight operations.")
            };
        });
    }

    private static string BuildMeterName(string prefix, string topic)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return topic;
        if (string.IsNullOrWhiteSpace(topic)) return prefix;
        if (topic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return topic;
        return $"{prefix}.{topic}";
    }

    public static string SanitizeInstrumentMetricName(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName)) return "operation";

        var sb = new StringBuilder(metricName.Length);
        foreach (var c in metricName)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.ToString();
    }

    public void Reset()
    {
        _cardinalityGuard.Reset();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (disposing)
        {
            _meter.Dispose();
            _instruments.Clear();
        }
    }

    private sealed class OperationInstruments
    {
        public required Histogram<double> Duration { get; init; }
        public required Counter<long> Total { get; init; }
        public required Counter<long> Items { get; init; }
        public required Counter<long> Exceptions { get; init; }
        public required UpDownCounter<long> Active { get; init; }
    }
}
