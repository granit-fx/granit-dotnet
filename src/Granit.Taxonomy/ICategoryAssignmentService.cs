using Granit.Taxonomy.Domain;

namespace Granit.Taxonomy;

/// <summary>
/// Service for assigning a single <see cref="Category"/> to a target aggregate
/// per ADR-054 (one category per <c>(TenantId, TargetType, TargetId)</c>,
/// opposite of <see cref="TagAssignment"/> which is many-to-many).
/// </summary>
public interface ICategoryAssignmentService
{
    /// <summary>
    /// Sets the category for <paramref name="targetType"/>/<paramref name="targetId"/>.
    /// If a previous assignment exists for the same target, the row is updated to
    /// point at <paramref name="categoryId"/> instead of creating a duplicate.
    /// </summary>
    /// <returns>The persisted assignment row.</returns>
    Task<CategoryAssignment> AssignAsync(
        Guid categoryId,
        string targetType,
        Guid targetId,
        Guid assignedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the category assignment for the target. Returns <c>false</c> if no row matched.</summary>
    Task<bool> UnassignAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the (single) category assigned to the target, or <c>null</c>.</summary>
    Task<Category?> GetForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the assignment for the given target. Called by the T5.1
    /// <c>EntityDeletedEto</c> cleanup handler when the target aggregate is hard-deleted.
    /// </summary>
    Task<int> RemoveAllAssignmentsAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);
}
