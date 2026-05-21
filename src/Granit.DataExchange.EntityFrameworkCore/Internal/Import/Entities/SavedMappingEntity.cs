using Granit.DataExchange.Import.Mapping;
using Granit.Domain;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;

/// <summary>
/// Persisted column mappings for a given import definition and tenant.
/// Enables the "Saved" tier of the mapping suggestion pipeline.
/// </summary>
internal sealed class SavedMappingEntity : IMultiTenant
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The import definition name (e.g. <c>"Acme.PatientImport"</c>).</summary>
    public required string DefinitionName { get; set; }

    /// <summary>Tenant identifier. <c>null</c> when multi-tenancy is not active.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Column mappings persisted as a JSON-owned collection.</summary>
    public required List<ImportColumnMapping> Mappings { get; set; }

    /// <summary>When the mappings were saved.</summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>Identifier of the user who saved the mappings.</summary>
    public required string SavedBy { get; set; }
}
