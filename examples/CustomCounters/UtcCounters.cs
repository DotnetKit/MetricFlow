namespace CustomCounters
{

    /// <summary>
    ///  UTC based MetricCounter implementation
    /// </summary>
    public class UtcCounters(string contextName) : MetricCountersBase<UtcCounter>(contextName)
    {

    }
}