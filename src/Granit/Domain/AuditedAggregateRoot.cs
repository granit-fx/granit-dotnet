namespace Granit.Domain;

/// <summary>
/// Aggregate root with full audit trail (creation + modification).
/// Inherits from <see cref="CreationAuditedAggregateRoot"/> and adds ModifiedAt/ModifiedBy.
/// </summary>
public abstract class AuditedAggregateRoot : CreationAuditedAggregateRoot
{
    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Identifier of the user who last modified the entity.</summary>
    public string? ModifiedBy { get; set; }
}
