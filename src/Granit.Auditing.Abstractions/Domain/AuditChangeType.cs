namespace Granit.Auditing.Domain;

/// <summary>
/// Type of change applied to an entity during an audited operation.
/// </summary>
public enum AuditChangeType
{
    /// <summary>A new entity was created.</summary>
    Created,

    /// <summary>An existing entity was modified.</summary>
    Modified,

    /// <summary>An entity was physically deleted.</summary>
    Deleted,

    /// <summary>An entity was soft-deleted (IsDeleted set to true).</summary>
    SoftDeleted,
}
