namespace Granit.Querying;

/// <summary>
/// Immutable metadata about a single declared column, built from <see cref="ColumnBuilder{TEntity}"/>.
/// </summary>
public sealed class ColumnDescriptor
{
    /// <summary>Property name on the entity (e.g. <c>"LastName"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the column property.</summary>
    public required Type ClrType { get; init; }

    /// <summary>User-facing label, or <c>null</c> to use the property name.</summary>
    public string? Label { get; init; }

    /// <summary>Display order in the UI (lower values first).</summary>
    public int Order { get; init; }

    /// <summary>Whether this column supports sorting. Default is <c>false</c>.</summary>
    public bool IsSortable { get; init; }

    /// <summary>Whether this column supports filtering. Default is <c>false</c>.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>Whether this column is visible by default. Default is <c>true</c>.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Display format hint (e.g. <c>"dd/MM/yyyy"</c>), or <c>null</c>.</summary>
    public string? Format { get; init; }

    /// <summary>
    /// Whether this column maps to an EF Core Shadow Property (not a CLR property).
    /// Shadow columns are accessed via <c>EF.Property&lt;T&gt;(entity, name)</c> instead of
    /// direct member access.
    /// </summary>
    public bool IsShadowProperty { get; init; }
}
