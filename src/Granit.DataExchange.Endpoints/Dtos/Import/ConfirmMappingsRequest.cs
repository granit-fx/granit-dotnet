using Granit.DataExchange.Import.Mapping;
using Granit.Domain;

namespace Granit.DataExchange.Endpoints.Dtos.Import;

/// <summary>
/// Request DTO for confirming column mappings on an import job.
/// </summary>
/// <param name="Mappings">Column-to-property mappings to confirm.</param>
/// <param name="ConcurrencyStamp">Stamp from the last read; must match the stored value (prevents lost updates).</param>
public sealed record ConfirmMappingsRequest(
    IReadOnlyList<ImportColumnMapping> Mappings,
    string ConcurrencyStamp) : IConcurrencyStampRequest;
