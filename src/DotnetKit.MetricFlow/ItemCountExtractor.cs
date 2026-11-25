using System.Globalization;

namespace DotnetKit.MetricFlow;

/// <summary>
/// Helper for extracting item and batch counts from tags and metadata dictionaries.
/// </summary>
internal static class ItemCountExtractor
{
    public static bool TryExtractItemCount(
        IReadOnlyDictionary<string, string>? tags,
        IReadOnlyDictionary<string, string>? metadata,
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

    public static bool TryExtractFromMetadata(IReadOnlyDictionary<string, string> metadata, out long items)
        => TryExtractFromDictionary(metadata, out items);

    public static bool TryExtractFromTags(IReadOnlyDictionary<string, string> tags, out long items)
        => TryExtractFromDictionary(tags, out items);

    private static bool TryExtractFromDictionary(IReadOnlyDictionary<string, string> values, out long items)
    {
        if (values.TryGetValue("items", out var itemsStr) && long.TryParse(itemsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out items)) return true;
        if (values.TryGetValue("count", out var countStr) && long.TryParse(countStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out items)) return true;
        if (values.TryGetValue("batch_size", out var batchStr) && long.TryParse(batchStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out items)) return true;

        foreach (var (key, value) in values)
        {
            if ((string.Equals(key, "items", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "count", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "batch_size", StringComparison.OrdinalIgnoreCase)) &&
                long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out items))
            {
                return true;
            }
        }

        items = 0;
        return false;
    }
}
