namespace DotnetKit.MetricFlow;

/// <summary>
/// Helper for extracting item and batch counts from tags and metadata dictionaries.
/// </summary>
internal static class ItemCountExtractor
{
    public static bool TryExtractItemCount(
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, long>? metadata,
        out long items)
    {
        if (metadata != null && TryExtractFromMetadata(metadata, out items))
        {
            return true;
        }

        if (tags != null && TryExtractFromTags(tags, out items))
        {
            return true;
        }

        items = 0;
        return false;
    }

    public static bool TryExtractFromMetadata(IReadOnlyDictionary<string, long> metadata, out long items)
    {
        if (metadata.TryGetValue("items", out items)) return true;
        if (metadata.TryGetValue("count", out items)) return true;
        if (metadata.TryGetValue("batch_size", out items)) return true;

        foreach (var (key, value) in metadata)
        {
            if (string.Equals(key, "items", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "batch_size", StringComparison.OrdinalIgnoreCase))
            {
                items = value;
                return true;
            }
        }

        items = 0;
        return false;
    }

    public static bool TryExtractFromTags(IReadOnlyDictionary<string, string> tags, out long items)
    {
        if (tags.TryGetValue("items", out var itemsStr) && long.TryParse(itemsStr, out items)) return true;
        if (tags.TryGetValue("count", out var countStr) && long.TryParse(countStr, out items)) return true;
        if (tags.TryGetValue("batch_size", out var batchStr) && long.TryParse(batchStr, out items)) return true;

        foreach (var (key, value) in tags)
        {
            if ((string.Equals(key, "items", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "batch_size", StringComparison.OrdinalIgnoreCase)) &&
                long.TryParse(value, out items))
            {
                return true;
            }
        }

        items = 0;
        return false;
    }
}
