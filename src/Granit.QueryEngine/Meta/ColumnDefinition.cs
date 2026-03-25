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
public sealed record ColumnDefinition(
    string Name,
    string Label,
    string Type,
    int Order,
    bool IsSortable,
    bool IsFilterable,
    bool IsVisible,
    string? Format);
