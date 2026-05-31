namespace Granit.DataExchange.Export;

/// <summary>
/// Declares what output formats an <c>IExportWriter</c> can handle beyond basic scalar rows.
/// </summary>
/// <param name="SupportsHierarchy">
/// When <c>true</c>, the writer can serialize nested/hierarchical values (objects, arrays, trees)
/// emitted by <c>ComplexField</c> selectors.
/// CSV and XLSX writers set this to <c>false</c>; JSON and XML writers set it to <c>true</c>.
/// </param>
public sealed record ExportFormatCapabilities(bool SupportsHierarchy)
{
    /// <summary>Capabilities for tabular-only writers (CSV, XLSX): hierarchy not supported.</summary>
    public static readonly ExportFormatCapabilities TabularOnly = new(SupportsHierarchy: false);

    /// <summary>Capabilities for structured writers (JSON, XML): full hierarchy supported.</summary>
    public static readonly ExportFormatCapabilities Structured = new(SupportsHierarchy: true);
}
