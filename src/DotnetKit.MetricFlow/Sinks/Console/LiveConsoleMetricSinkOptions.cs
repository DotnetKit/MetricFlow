namespace DotnetKit.MetricFlow.Sinks.Console;

/// <summary>
/// Configuration options for <see cref="LiveConsoleMetricSink"/>.
/// </summary>
public class LiveConsoleMetricSinkOptions
{
    /// <summary>
    /// The refresh interval for updating the live console dashboard. Defaults to 1 second.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The title displayed in the dashboard header banner. Defaults to "MetricFlow Live Pipeline Dashboard".
    /// </summary>
    public string Title { get; set; } = "MetricFlow Live Pipeline Dashboard";

    /// <summary>
    /// The <see cref="TextWriter"/> to write dashboard output to. If null, defaults to <see cref="System.Console.Out"/>.
    /// </summary>
    public TextWriter? OutputWriter { get; set; }

    /// <summary>
    /// Whether to enable ANSI color formatting in the dashboard. Defaults to true.
    /// </summary>
    public bool Colorize { get; set; } = true;

    /// <summary>
    /// Whether to render categorical dimension breakdowns as sub-rows beneath each stage. Defaults to true.
    /// </summary>
    public bool ShowDimensionBreakdown { get; set; } = true;

    /// <summary>
    /// Maximum number of breakdown values to display per dimension tag. Defaults to 4.
    /// </summary>
    public int MaxDimensionValuesToRender { get; set; } = 4;

    /// <summary>
    /// Underlying table rendering options for stage formatting and threshold color rules.
    /// </summary>
    public ConsoleMetricSinkOptions TableOptions { get; set; } = new()
    {
        DisplayMode = ConsoleDisplayMode.Table,
        ShowTableHeaders = true
    };
}
