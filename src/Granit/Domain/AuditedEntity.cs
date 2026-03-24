namespace Granit.Domain;

/// <summary>
/// Entity with full audit trail (creation + modification).
/// Inherits from <see cref="CreationAuditedEntity"/> and adds ModifiedAt/ModifiedBy.
/// </summary>
public abstract class AuditedEntity : CreationAuditedEntity
{
    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Identifier of the user who last modified the entity.</summary>
    public string? ModifiedBy { get; set; }
}
