using Granit.DataLookup.Descriptors;

namespace Granit.QueryEngine;

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

    /// <summary>Localization key for the label, or <c>null</c> to skip localization.</summary>
    public string? LabelKey { get; init; }

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
    /// ISO 4217 alpha-3 currency code (e.g. <c>"EUR"</c>, <c>"USD"</c>) when
    /// the column carries a monetary amount; <c>null</c> for non-monetary
    /// columns. Set via <see cref="ColumnBuilder{TEntity}.Currency(string)"/>.
    /// Drives the <c>"Currency"</c> value-kind on dashboard widget snapshots
    /// so the frontend formats values with the right symbol + locale.
    /// </summary>
    public string? CurrencyCode { get; init; }

    /// <summary>
    /// Name of the sibling column carrying this column's ISO 4217 code per row, when the amount is
    /// multi-currency; <c>null</c> otherwise. Set via
    /// <see cref="ColumnBuilder{TEntity}.Currency(System.Linq.Expressions.Expression{System.Func{TEntity, string}})"/>.
    /// Mutually exclusive with <see cref="CurrencyCode"/> (a fixed code wins on the frontend).
    /// </summary>
    public string? CurrencyCodeField { get; init; }

    /// <summary>
    /// Semantic display-type of the column (<c>Currency</c>, <c>Percentage</c>, <c>Url</c>, …),
    /// or <c>null</c> to let the frontend fall back to the CLR type. Set via the
    /// <see cref="ColumnBuilder{TEntity}"/> helpers (<c>Currency</c>, <c>Percentage</c>,
    /// <c>Url</c>, …) or <see cref="ColumnBuilder{TEntity}.ValueKind(QueryEngine.ValueKind)"/>.
    /// Surfaced on the wire <see cref="Meta.ColumnDefinition"/> so tables pick the right renderer.
    /// </summary>
    public ValueKind? ValueKind { get; init; }

    /// <summary>
    /// Whether this column maps to an EF Core Shadow Property (not a CLR property).
    /// Shadow columns are accessed via <c>EF.Property&lt;T&gt;(entity, name)</c> instead of
    /// direct member access.
    /// </summary>
    public bool IsShadowProperty { get; init; }

    /// <summary>
    /// Optional data-lookup descriptor propagated to <c>FilterableField.Lookup</c> in the
    /// query metadata. Set via <see cref="ColumnBuilder{TEntity}.Lookup(string, LookupKind, string?, IReadOnlyList{string}?)"/>
    /// or the <see cref="LookupDescriptor"/> overload.
    /// </summary>
    public LookupDescriptor? Lookup { get; init; }
}
