using DotnetKit.MetricFlow.Core.Abstractions;

namespace DotnetKit.MetricFlow.Core.Extensions;

public static class PerfCountersExtensions
{
    public static IEnumerable<string> Render<T>(this IMetricCounters<T> counters)
        where T : ICounter
    {
        foreach (var counter in counters.Get().OrderBy(c => c.Key))
        {
            yield return RenderDuration($"{counter.Key} ", counter.Value);
        }
    }

    private static string RenderDuration(string text, ICounter counter)
    {
        return $"{text} {Math.Round(counter.AverageDuration.TotalMilliseconds / 1000, 3)} seconds";
    }

    public static void WriteToConsole<T>(this IMetricCounters<T> counters)
    where T : ICounter
    {
        Console.WriteLine("======================================");
        Console.WriteLine(counters.Topic);
        Console.WriteLine();
        foreach (var counter in counters.Get().OrderBy(c => c.Key))
        {
            WriteDurationLines($"{counter.Key} ", counter.Value);
        }
        Console.WriteLine("======================================");
    }

    private static void WriteDurationLines(string text, ICounter counter)
    {
        Console.WriteLine(text);
        Console.WriteLine($"Count (in, out): {counter.InCount} / {counter.OutCount}");
        Console.WriteLine($"Avg duration: {counter.AverageDuration.TotalMilliseconds} ms");
        Console.WriteLine($"Duration (min, max) : {counter.MinDuration.TotalMilliseconds} ms / {counter.MaxDuration.TotalMilliseconds} ms");
        Console.WriteLine($"Total duration: {counter.TotalDuration.TotalMilliseconds} ms");
        Console.WriteLine();
    }
}