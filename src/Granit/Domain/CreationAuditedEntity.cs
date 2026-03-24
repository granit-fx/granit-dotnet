namespace Granit.Domain;

/// <summary>
/// Entity with creation-only audit trail.
/// Inherits from <see cref="Entity"/> and adds CreatedAt/CreatedBy.
/// </summary>
public abstract class CreationAuditedEntity : Entity
{
    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identifier of the user who created the entity.</summary>
    public string CreatedBy { get; set; } = string.Empty;
}
