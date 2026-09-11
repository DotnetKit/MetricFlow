namespace DotnetKit.MetricFlow.Tracker.Abstractions
{
    public readonly ref struct InContext
    {
        public string MetricName { get; }
        public IReadOnlyDictionary<string, string>? Tags { get; }
        public DateTime UtcTimestamp { get; }

        public InContext(string metricName, IReadOnlyDictionary<string, string>? tags = null, DateTime? utcTimestamp = null)
        {
            MetricName = metricName;
            Tags = tags;
            UtcTimestamp = utcTimestamp ?? DateTime.UtcNow;
        }
    }
}
