namespace Granit.Domain;

/// <summary>
/// Interface for GDPR soft deletion.
/// Marked entities are no longer returned by standard queries
/// but remain in the database for the ISO 27001 audit trail.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Indicates whether the entity has been soft-deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>Soft deletion timestamp (UTC).</summary>
    DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Identifier of the user who deleted the entity.</summary>
    string? DeletedBy { get; set; }
}
