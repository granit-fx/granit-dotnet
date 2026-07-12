using Granit.Auditing.Attributes;
using Granit.Domain;

namespace Granit.Auditing.Domain;

/// <summary>
/// Records a single entity change within an <see cref="AuditEntry"/>.
/// </summary>
/// <remarks>
/// <see cref="IMultiTenant"/> with the tenant id denormalized from the parent
/// <see cref="AuditEntry"/>: the automatic multi-tenant query filter does not propagate
/// through a parent join, so a bare <c>Set&lt;AuditEntityChange&gt;()</c> (query engine,
/// exports) would otherwise return cross-tenant rows.
/// </remarks>
[AuditIgnore]
public class AuditEntityChange : Entity, IMultiTenant
{
    /// <summary>Foreign key to the parent <see cref="AuditEntry"/>.</summary>
    public Guid AuditEntryId { get; set; }

    /// <summary>Tenant identifier, denormalized from the parent <see cref="AuditEntry"/>.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>CLR type name of the affected entity.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity (serialized as string).</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Type of change applied to the entity.</summary>
    public AuditChangeType ChangeType { get; set; }

    /// <summary>Property-level changes for this entity.</summary>
    public ICollection<AuditPropertyChange> PropertyChanges { get; set; } = [];
}
