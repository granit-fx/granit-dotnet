namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about a declared aggregate (used in grouped queries).
/// </summary>
public sealed record AggregateDescriptor
{
    /// <summary>Property name to aggregate (e.g. <c>"Amount"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the property.</summary>
    public required Type ClrType { get; init; }

    /// <summary>The aggregate function to apply.</summary>
    public required AggregateFunction Function { get; init; }

    /// <summary>Alias for the aggregate result (e.g. <c>"totalAmount"</c>).</summary>
    public required string Alias { get; init; }
}
