using System.Text;

namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public record CounterValues(
        long InCount,
        long OutCount,
        TimeSpan TotalDuration,
        TimeSpan AverageDuration,
        TimeSpan MinDuration,
        TimeSpan MaxDuration
       )
    {
        /// <summary>
        /// Overrided default representation of a counter values
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Count (in, out): {InCount} / {OutCount}");
            sb.AppendLine($"Avg duration: {AverageDuration.TotalMilliseconds} ms");
            sb.AppendLine($"Duration (min, max) : {MinDuration.TotalMilliseconds} ms / {MaxDuration.TotalMilliseconds} ms");
            sb.AppendLine($"Total duration: {TotalDuration.TotalMilliseconds} ms");
            return sb.ToString();
        }
    }
}