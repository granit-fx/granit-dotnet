namespace Granit.DataExchange.Endpoints.Dtos.Export;

/// <summary>
/// Request DTO for creating an export job.
/// </summary>
/// <param name="DefinitionName">The export definition name (e.g. <c>"Acme.PatientExport"</c>).</param>
/// <param name="Format">Output format (<c>"xlsx"</c> or <c>"csv"</c>).</param>
/// <param name="SelectedFields">
/// Ordered list of field property paths to include. <c>null</c> means all fields.
/// </param>
/// <param name="IncludeIdForImport">Whether to include the entity ID for roundtrip import.</param>
/// <param name="Sort">
/// Comma-separated sort specification (e.g. <c>"-createdAt,lastName"</c>).
/// </param>
/// <param name="Filter">
/// Filter criteria using the <c>filter[field.op]=value</c> syntax.
/// </param>
/// <param name="Presets">
/// Active preset names, keyed by filter group name.
/// </param>
/// <param name="Search">
/// Free-text search applied to the definition's global search properties.
/// </param>
public sealed record CreateExportJobRequest(
    string DefinitionName,
    string Format,
    IReadOnlyList<string>? SelectedFields,
    bool IncludeIdForImport,
    string? Sort = null,
    IReadOnlyDictionary<string, string>? Filter = null,
    IReadOnlyDictionary<string, string>? Presets = null,
    string? Search = null);
