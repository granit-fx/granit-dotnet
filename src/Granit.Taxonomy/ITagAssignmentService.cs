using Granit.Taxonomy.Domain;

namespace Granit.Taxonomy;

/// <summary>
/// Service for assigning <see cref="Tag"/>s to target aggregate roots through the
/// polymorphic <c>TagAssignment</c> table introduced by ADR-054 (T2.2).
/// </summary>
public interface ITagAssignmentService
{
    /// <summary>
    /// Idempotent assignment. If <c>(TenantId, TagId, TargetType, TargetId)</c> already
    /// exists, the existing row is returned. Otherwise a new <see cref="TagAssignment"/>
    /// is created.
    /// </summary>
    /// <returns>A tuple of the assignment and a flag indicating whether the row was newly created.</returns>
    Task<(TagAssignment Assignment, bool Created)> AssignAsync(
        Guid tagId,
        string targetType,
        Guid targetId,
        Guid assignedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the assignment row matching the given triplet.
    /// </summary>
    /// <returns><c>true</c> when a row was deleted; <c>false</c> when none matched.</returns>
    Task<bool> UnassignAsync(
        Guid tagId,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every <see cref="Tag"/> currently assigned to the target.</summary>
    Task<IReadOnlyList<Tag>> ListForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every assignment row for the given target. Called by the T5.1
    /// <c>EntityDeletedEto</c> cleanup handler when a target aggregate is hard-deleted.
    /// </summary>
    /// <returns>The number of rows removed.</returns>
    Task<int> RemoveAllAssignmentsAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);
}
