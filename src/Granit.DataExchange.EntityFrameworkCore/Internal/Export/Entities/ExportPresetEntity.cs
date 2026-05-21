using Granit.Domain;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Entities;

/// <summary>
/// Persisted export preset for a given export definition and tenant.
/// Stores the user's field selection, format preference, and roundtrip option.
/// </summary>
internal sealed class ExportPresetEntity : IMultiTenant
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The export definition name (e.g. <c>"Acme.PatientExport"</c>).</summary>
    public required string DefinitionName { get; set; }

    /// <summary>User-facing preset name (e.g. <c>"Export mensuel"</c>).</summary>
    public required string PresetName { get; set; }

    /// <summary>Tenant identifier. <c>null</c> when multi-tenancy is not active.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Selected field property paths, persisted as a JSON primitive collection.</summary>
    public required List<string> Fields { get; set; }

    /// <summary>Output format (<c>"xlsx"</c> or <c>"csv"</c>).</summary>
    public required string Format { get; set; }

    /// <summary>Whether to include the entity ID for roundtrip import.</summary>
    public bool IncludeIdForImport { get; set; }

    /// <summary>When the preset was saved.</summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>Identifier of the user who saved the preset.</summary>
    public required string SavedBy { get; set; }
}
