namespace DotnetKit.MetricFlow.Abstractions;

/// <summary>
/// Represents a fluent builder abstraction.
/// </summary>
/// <typeparam name="TContext">The underlying context type.</typeparam>
public interface IFluentBuilder<out TContext>
{
    /// <summary>
    /// Gets the underlying context.
    /// </summary>
    TContext Current { get; }
}

/// <summary>
/// Represents an extensible fluent builder abstraction.
/// </summary>
/// <typeparam name="TExtension">The extension point type.</typeparam>
/// <typeparam name="TContext">The underlying context type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "Generic type parameter is part of the extensible fluent builder contract")]
public interface IFluentBuilder<in TExtension, out TContext>
{
    /// <summary>
    /// Gets the underlying context.
    /// </summary>
    TContext Current { get; }
}