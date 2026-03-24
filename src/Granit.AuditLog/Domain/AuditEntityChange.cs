using Granit.Domain;

namespace Granit.AuditLog.Domain;

/// <summary>
/// Records a single entity change within an <see cref="AuditLogEntry"/>.
/// </summary>
public class AuditEntityChange : Entity
{
    /// <summary>Foreign key to the parent <see cref="AuditLogEntry"/>.</summary>
    public Guid AuditLogEntryId { get; set; }

    /// <summary>CLR type name of the affected entity.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity (serialized as string).</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Type of change applied to the entity.</summary>
    public AuditChangeType ChangeType { get; set; }

    /// <summary>Property-level changes for this entity.</summary>
    public ICollection<AuditPropertyChange> PropertyChanges { get; set; } = [];
}
