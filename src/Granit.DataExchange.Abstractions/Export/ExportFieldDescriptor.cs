namespace Granit.DataExchange.Export;

/// <summary>
/// Immutable metadata about a single exportable field, built from <see cref="ExportFieldBuilder"/>.
/// </summary>
/// <param name="PropertyPath">
/// Dot-separated property path on the entity (e.g. <c>"Email"</c> or <c>"Company.Name"</c>).
/// </param>
/// <param name="ClrTypeName">CLR type name (e.g. <c>"String"</c>, <c>"DateTimeOffset"</c>).</param>
/// <param name="Header">Column header for the export file, or <c>null</c> to use <paramref name="PropertyPath"/>.</param>
/// <param name="Format">Display format (e.g. <c>"dd/MM/yyyy"</c>, <c>"#,##0.00"</c>), or <c>null</c>.</param>
/// <param name="Order">Display order (lower values first).</param>
/// <param name="IsNavigation">Whether this field traverses a navigation property.</param>
public sealed record ExportFieldDescriptor(
    string PropertyPath,
    string ClrTypeName,
    string? Header,
    string? Format,
    int Order,
    bool IsNavigation);
