using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;

namespace Granit.DataExchange.Endpoints.Dtos.Import;

/// <summary>
/// Response DTO for the import preview (headers, sample rows, mapping suggestions, and field metadata).
/// </summary>
public sealed record ImportPreviewResponse(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> PreviewRows,
    IReadOnlyList<ImportColumnMapping> Suggestions,
    IReadOnlyList<ImportFieldMetadata> FieldMetadata);
