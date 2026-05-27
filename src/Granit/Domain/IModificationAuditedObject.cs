namespace Granit.Domain;

/// <summary>
/// Marker for entities that carry a modification audit trail (ISO 27001).
/// Implemented by both the <see cref="AuditedEntity"/> and the
/// <see cref="AuditedAggregateRoot"/> hierarchies, whose base classes are
/// otherwise disjoint, so a single interceptor branch can populate the fields
/// regardless of whether the object is a plain entity or an aggregate root.
/// </summary>
public interface IModificationAuditedObject
{
    /// <summary>Last modification timestamp (UTC).</summary>
    DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Identifier of the user who last modified the entity.</summary>
    string? ModifiedBy { get; set; }
}
