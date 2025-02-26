﻿namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public interface ICounter
    {
        string Name { get; }
        DateTime TimeStamp { get; }
        CounterValues Values { get; }
        long Inc();
        long Dec();
    }
}