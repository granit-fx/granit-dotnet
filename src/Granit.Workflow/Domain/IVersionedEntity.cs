using Granit.Core.Domain;

namespace Granit.Workflow.Domain;

/// <summary>
/// Interface for entities with a versioned publication lifecycle.
/// Combines <see cref="IVersioned"/> (structural versioning) with
/// <see cref="IPublishable"/> (global query filter) and adds a
/// <see cref="LifecycleStatus"/> for workflow state management.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IVersioned"/> for <c>VersionId</c>/<c>Version</c>
/// and <see cref="IPublishable"/> for the global query filter
/// (<c>WHERE IsPublished = true</c>).
/// </para>
/// <para>
/// At most one version per <see cref="IVersioned.VersionId"/> may have
/// <see cref="WorkflowLifecycleStatus.Published"/> at any time.
/// This invariant is enforced by a unique filtered index in PostgreSQL.
/// </para>
/// </remarks>
public interface IVersionedEntity : IVersioned, IPublishable
{
    /// <summary>Lifecycle status of this version.</summary>
    WorkflowLifecycleStatus LifecycleStatus { get; set; }
}
