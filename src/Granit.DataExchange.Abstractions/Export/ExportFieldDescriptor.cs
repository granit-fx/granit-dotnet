namespace Granit.DataExchange.Export;

/// <summary>
/// Immutable metadata about a single exportable field, built from <see cref="ExportFieldBuilder"/>.
/// </summary>
/// <param name="PropertyPath">
/// Dot-separated property path on the entity (e.g. <c>"Email"</c> or <c>"Company.Name"</c>).
/// For complex fields this is the name passed to <c>ComplexField(name, …)</c>.
/// </param>
/// <param name="ClrTypeName">CLR type name (e.g. <c>"String"</c>, <c>"DateTimeOffset"</c>).</param>
/// <param name="Header">Column header for the export file, or <c>null</c> to use <paramref name="PropertyPath"/>.</param>
/// <param name="Format">Display format (e.g. <c>"dd/MM/yyyy"</c>, <c>"#,##0.00"</c>), or <c>null</c>.</param>
/// <param name="Order">Display order (lower values first).</param>
/// <param name="IsNavigation">Whether this field traverses a navigation property.</param>
/// <param name="RequiresHierarchy">
/// When <c>true</c>, this field carries a complex/nested value that requires a structured writer
/// (JSON, XML). Tabular writers (CSV, XLSX) will reject or skip it according to
/// <see cref="IExportDefinitionDescriptor.OnIncompatibleField"/>.
/// </param>
/// <param name="ValueSelector">
/// A boxed <c>Func&lt;object, object?&gt;</c> that extracts the complex value from the entity at runtime.
/// <c>null</c> for scalar and navigation fields (resolved via reflection on <see cref="PropertyPath"/>).
/// </param>
/// <param name="SelectorType">
/// The exact <c>TValue</c> type captured from <c>ComplexField&lt;TValue&gt;</c>.
/// Used by structured writers (JSON, XML) to serialize the complex value under its declared contract
/// rather than <c>typeof(object)</c>, preserving polymorphic sub-type information.
/// <c>null</c> for scalar and navigation fields.
/// </param>
public sealed record ExportFieldDescriptor(
    string PropertyPath,
    string ClrTypeName,
    string? Header,
    string? Format,
    int Order,
    bool IsNavigation,
    bool RequiresHierarchy = false,
    Func<object, object?>? ValueSelector = null,
    Type? SelectorType = null);
