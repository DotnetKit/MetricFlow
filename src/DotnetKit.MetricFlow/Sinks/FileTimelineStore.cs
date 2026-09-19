using System.Text;
using System.Text.Json;
using DotnetKit.MetricFlow.Abstractions;
using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Extensions;

namespace DotnetKit.MetricFlow.Sinks;

/// <summary>
/// A file-based timeline persistence store that writes entries as append-only Newline-Delimited JSON (NDJSON)
/// with support for date-based rollups and multi-instance directory partitioning.
/// </summary>
public sealed class FileTimelineStore : IMetricTimelineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly FileTimelineStoreOptions _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string Name { get; }

    public FileTimelineStore(FileTimelineStoreOptions? options = null, string name = "FileTimelineStore")
    {
        _options = options ?? new FileTimelineStoreOptions();
        Name = name;
        Directory.CreateDirectory(_options.DirectoryPath);
    }

    public async ValueTask EmitAsync(IReadOnlyList<MetricTimelineEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        // Group entries by destination file path
        var groupedByFile = entries.GroupBy(GetFilePathForEntry);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var group in groupedByFile)
            {
                var filePath = group.Key;
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);

                foreach (var entry in group)
                {
                    var record = PersistedRecord.FromEntry(entry);
                    var line = JsonSerializer.Serialize(record, JsonOptions);
                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                }

                if (_options.AutoFlush)
                {
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask<IReadOnlyList<MetricTimelineEntry>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MetricTimelineEntry>();
        if (!Directory.Exists(_options.DirectoryPath))
        {
            return result;
        }

        var files = Directory.GetFiles(_options.DirectoryPath, "*.ndjson", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                PersistedRecord? record;
                try
                {
                    record = JsonSerializer.Deserialize<PersistedRecord>(line, JsonOptions);
                }
                catch
                {
                    continue; // Skip malformed lines
                }

                if (record == null) continue;

                if (record.PeriodEnd >= fromUtc && record.PeriodStart <= toUtc)
                {
                    if (metricName == null || string.Equals(record.MetricName, metricName, StringComparison.OrdinalIgnoreCase))
                    {
                        var entry = record.ToEntry();
                        if (entry != null)
                        {
                            result.Add(entry);
                        }
                    }
                }
            }
        }

        return result.OrderBy(e => e.PeriodStart).ToList();
    }

    public async ValueTask<IReadOnlyList<IMetricSnapshot>> GetAggregatedSnapshotsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? metricName = null,
        CancellationToken cancellationToken = default)
    {
        var timeline = await GetTimelineAsync(fromUtc, toUtc, metricName, cancellationToken).ConfigureAwait(false);
        var deltas = timeline.Select(t => t.Delta).ToList();
        return deltas.AggregateSnapshots();
    }

    private string GetFilePathForEntry(MetricTimelineEntry entry)
    {
        var dateStr = entry.PeriodEnd.ToString("yyyy-MM-dd");
        var fileName = $"timeline-{dateStr}.ndjson";

        if (_options.PartitionByInstance && entry.Resource != null && !string.IsNullOrWhiteSpace(entry.Resource.InstanceId))
        {
            var sanitizedInstance = SanitizeFileName(entry.Resource.InstanceId);
            return Path.Combine(_options.DirectoryPath, sanitizedInstance, fileName);
        }

        return Path.Combine(_options.DirectoryPath, fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }

    internal sealed class PersistedRecord
    {
        public DateTimeOffset PeriodStart { get; set; }
        public DateTimeOffset PeriodEnd { get; set; }
        public string MetricName { get; set; } = "";
        public string CounterName { get; set; } = "";
        public string SnapshotKind { get; set; } = "";
        public JsonElement Cumulative { get; set; }
        public JsonElement Delta { get; set; }
        public ResourceMetadata? Resource { get; set; }
        public Dictionary<string, string>? Tags { get; set; }

        public static PersistedRecord FromEntry(MetricTimelineEntry entry)
        {
            var kind = entry.Cumulative switch
            {
                DurationSnapshot => "Duration",
                MemorySnapshot => "Memory",
                ExceptionSnapshot => "Exception",
                _ => entry.Cumulative.GetType().Name
            };

            var cumJson = JsonSerializer.SerializeToElement(entry.Cumulative, entry.Cumulative.GetType(), JsonOptions);
            var deltaJson = JsonSerializer.SerializeToElement(entry.Delta, entry.Delta.GetType(), JsonOptions);

            return new PersistedRecord
            {
                PeriodStart = entry.PeriodStart,
                PeriodEnd = entry.PeriodEnd,
                MetricName = entry.MetricName,
                CounterName = entry.CounterName,
                SnapshotKind = kind,
                Cumulative = cumJson,
                Delta = deltaJson,
                Resource = entry.Resource,
                Tags = entry.Tags != null ? new Dictionary<string, string>(entry.Tags) : null
            };
        }

        public MetricTimelineEntry? ToEntry()
        {
            IMetricSnapshot? cum = null;
            IMetricSnapshot? delta = null;

            switch (SnapshotKind)
            {
                case "Duration":
                    cum = JsonSerializer.Deserialize<DurationSnapshot>(Cumulative.GetRawText(), JsonOptions);
                    delta = JsonSerializer.Deserialize<DurationSnapshot>(Delta.GetRawText(), JsonOptions);
                    break;
                case "Memory":
                    cum = JsonSerializer.Deserialize<MemorySnapshot>(Cumulative.GetRawText(), JsonOptions);
                    delta = JsonSerializer.Deserialize<MemorySnapshot>(Delta.GetRawText(), JsonOptions);
                    break;
                case "Exception":
                    cum = JsonSerializer.Deserialize<ExceptionSnapshot>(Cumulative.GetRawText(), JsonOptions);
                    delta = JsonSerializer.Deserialize<ExceptionSnapshot>(Delta.GetRawText(), JsonOptions);
                    break;
            }

            if (cum == null || delta == null) return null;

            return new MetricTimelineEntry(
                PeriodStart: PeriodStart,
                PeriodEnd: PeriodEnd,
                Cumulative: cum,
                Delta: delta,
                Resource: Resource,
                Tags: Tags
            );
        }
    }
}
