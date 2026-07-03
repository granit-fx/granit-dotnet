namespace Granit.QueryEngine.Meta;

/// <summary>
/// Column metadata for frontend auto-configuration.
/// </summary>
/// <param name="Name">Property name (e.g. <c>"lastName"</c>).</param>
/// <param name="Label">User-facing label.</param>
/// <param name="Type">CLR type name (e.g. <c>"String"</c>, <c>"Int32"</c>).</param>
/// <param name="Order">Display order.</param>
/// <param name="IsSortable">Whether sorting is allowed.</param>
/// <param name="IsFilterable">Whether filtering is allowed.</param>
/// <param name="IsVisible">Whether the column is visible by default.</param>
/// <param name="Format">Display format hint, or <c>null</c>.</param>
/// <param name="ValueKind">
/// Semantic display-type (<c>"Currency"</c>, <c>"Percentage"</c>, <c>"Url"</c>, …), or <c>null</c>
/// to fall back to <paramref name="Type"/>. Lets the table pick a renderer from the column itself.
/// </param>
/// <param name="CurrencyCode">
/// Fixed ISO 4217 code accompanying a <c>Currency</c> <paramref name="ValueKind"/>, or <c>null</c>.
/// </param>
/// <param name="CurrencyCodeField">
/// Name of the sibling column carrying the ISO 4217 code per row (multi-currency amounts), or
/// <c>null</c>. The frontend prefers <paramref name="CurrencyCode"/> when both are set.
/// </param>
public sealed record ColumnDefinition(
    string Name,
    string Label,
    string Type,
    int Order,
    bool IsSortable,
    bool IsFilterable,
    bool IsVisible,
    string? Format,
    ValueKind? ValueKind = null,
    string? CurrencyCode = null,
    string? CurrencyCodeField = null);
