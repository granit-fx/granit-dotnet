namespace Granit.Domain;

/// <summary>
/// Aggregate root with full audit trail including soft delete (GDPR).
/// Inherits from <see cref="AuditedAggregateRoot"/> and implements <see cref="ISoftDeletable"/>.
/// </summary>
public abstract class FullAuditedAggregateRoot : AuditedAggregateRoot, ISoftDeletable
{
    /// <summary>Indicates whether the entity has been soft-deleted.</summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>Soft deletion timestamp (UTC).</summary>
    public virtual DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Identifier of the user who deleted the entity.</summary>
    public virtual string? DeletedBy { get; set; }
}
