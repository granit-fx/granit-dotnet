namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about a property allowed for group-by operations.
/// </summary>
public sealed record GroupByDescriptor
{
    /// <summary>Property name (e.g. <c>"Status"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the property.</summary>
    public required Type ClrType { get; init; }
}
