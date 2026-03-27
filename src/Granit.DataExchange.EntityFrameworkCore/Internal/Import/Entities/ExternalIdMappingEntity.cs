using Granit.Domain;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;

/// <summary>
/// Maps an external identifier from a source system to an internal entity ID.
/// Used by the <c>ExternalIdResolver</c> for roundtrip INSERT/UPDATE resolution.
/// </summary>
internal sealed class ExternalIdMappingEntity : IMultiTenant
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The import definition name.</summary>
    public required string DefinitionName { get; set; }

    /// <summary>The external identifier from the source system.</summary>
    public required string ExternalId { get; set; }

    /// <summary>The internal entity identifier in the application database.</summary>
    public Guid InternalId { get; set; }

    /// <summary>Tenant identifier. <c>null</c> when multi-tenancy is not active.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>When the mapping was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
