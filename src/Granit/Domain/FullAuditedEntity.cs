namespace Granit.Domain;

/// <summary>
/// Entity with a full audit trail including soft deletion (GDPR).
/// Inherits from <see cref="AuditedEntity"/> and implements <see cref="ISoftDeletable"/>.
/// </summary>
public abstract class FullAuditedEntity : AuditedEntity, ISoftDeletable
{
    /// <summary>Indicates whether the entity is soft-deleted.</summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>Soft deletion timestamp (UTC).</summary>
    public virtual DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Identifier of the user who deleted the entity.</summary>
    public virtual string? DeletedBy { get; set; }
}
