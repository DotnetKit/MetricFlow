namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Specifies the time unit used to format durations in metric sinks.
/// </summary>
public enum DurationUnit
{
    /// <summary>
    /// Automatically selects the most appropriate unit based on duration magnitude:
    /// &lt; 1s =&gt; milliseconds (ms), &lt; 60s =&gt; seconds (s), &lt; 60m =&gt; minutes (m), &gt;= 1h =&gt; hours (h).
    /// </summary>
    Auto,

    /// <summary>
    /// Always displays in milliseconds (e.g. "85.20 ms").
    /// </summary>
    Milliseconds,

    /// <summary>
    /// Always displays in seconds (e.g. "1.45 s").
    /// </summary>
    Seconds,

    /// <summary>
    /// Always displays in minutes (e.g. "2.10 m").
    /// </summary>
    Minutes,

    /// <summary>
    /// Always displays in hours (e.g. "1.25 h").
    /// </summary>
    Hours
}

/// <summary>
/// Specifies the memory unit used to format byte counts in metric sinks.
/// </summary>
public enum MemoryUnit
{
    /// <summary>
    /// Automatically selects the most appropriate unit based on byte count:
    /// &lt; 1 KB =&gt; B, &lt; 1 MB =&gt; KB, &lt; 1 GB =&gt; MB, &gt;= 1 GB =&gt; GB.
    /// </summary>
    Auto,

    /// <summary>
    /// Always displays in raw bytes (e.g. "1048576 B").
    /// </summary>
    Bytes,

    /// <summary>
    /// Always displays in kilobytes (e.g. "1024.00 KB").
    /// </summary>
    Kilobytes,

    /// <summary>
    /// Always displays in megabytes (e.g. "1.00 MB").
    /// </summary>
    Megabytes,

    /// <summary>
    /// Always displays in gigabytes (e.g. "1.50 GB").
    /// </summary>
    Gigabytes
}
