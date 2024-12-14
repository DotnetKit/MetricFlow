namespace DotnetKit.MetricFlow.Core.Extensions
{
    internal static class DictionaryExtensions
    {
        public static string ToFormattedString(this Dictionary<string, string> dict, string prefix)
        {
            return $"{prefix}:  {string.Join(", ", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}";
        }
    }
}