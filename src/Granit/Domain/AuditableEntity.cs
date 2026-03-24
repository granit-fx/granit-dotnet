namespace Granit.Domain;

/// <summary>
/// Base class for all entities with an ISO 27001 audit trail.
/// Provides creation/modification traceability fields.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>Unique identifier of the entity.</summary>
    public Guid Id { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identifier of the user who created the entity.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Identifier of the user who last modified the entity.</summary>
    public string? ModifiedBy { get; set; }
}
