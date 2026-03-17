namespace Granit.AuditLog.Domain;

/// <summary>
/// Type of change applied to an entity during an audited operation.
/// </summary>
public enum AuditChangeType
{
    /// <summary>A new entity was created.</summary>
    Created = 0,

    /// <summary>An existing entity was modified.</summary>
    Modified = 1,

    /// <summary>An entity was physically deleted.</summary>
    Deleted = 2,

    /// <summary>An entity was soft-deleted (IsDeleted set to true).</summary>
    SoftDeleted = 3,
}
