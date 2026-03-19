namespace Granit.Workflow.Domain;

/// <summary>
/// Lifecycle status for versioned workflow entities implementing <see cref="IVersionedEntity"/>.
/// </summary>
public enum WorkflowLifecycleStatus
{
    /// <summary>Being edited. Not visible to standard queries (filtered by IPublishable).</summary>
    Draft = 0,

    /// <summary>Submitted for review. Awaiting approval from a user with the required permission.</summary>
    PendingReview = 1,

    /// <summary>
    /// Active published version. Exactly one per <see cref="IVersionedEntity.VersionId"/>
    /// at any time (enforced by unique filtered index).
    /// Maps to <c>IPublishable.IsPublished = true</c>.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Former published version, superseded by a newer publication.
    /// Preserved indefinitely for ISO 27001 audit trail (3-year retention).
    /// </summary>
    Archived = 3,
}
