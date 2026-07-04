using Granit.Domain;

namespace Granit.Workflow.Domain;

/// <summary>
/// Lifecycle status for versioned workflow entities implementing <see cref="IVersionedEntity"/>.
/// </summary>
public enum WorkflowLifecycleStatus
{
    /// <summary>Being edited. Not visible to standard queries (filtered by IPublishable).</summary>
    Draft,

    /// <summary>Submitted for review. Awaiting approval from a user with the required permission.</summary>
    PendingReview,

    /// <summary>
    /// Active published version. Exactly one per <see cref="IVersioned.VersionId"/>
    /// at any time (enforced by unique filtered index).
    /// Maps to <c>IPublishable.IsPublished = true</c>.
    /// </summary>
    Published,

    /// <summary>
    /// Former published version, superseded by a newer publication.
    /// Preserved indefinitely for ISO 27001 audit trail (3-year retention).
    /// </summary>
    Archived,
}
