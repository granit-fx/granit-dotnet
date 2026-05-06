using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>
/// Raised when a target's assigned category changes. Covers initial assignment
/// (<see cref="OldCategoryId"/> == <c>null</c>), re-assignment to a different
/// category, and unassignment (<see cref="NewCategoryId"/> == <c>null</c>).
/// </summary>
public sealed record CategoryAssignmentChangedEvent(
    Guid? TenantId,
    string TargetType,
    Guid TargetId,
    Guid? OldCategoryId,
    Guid? NewCategoryId) : IDomainEvent;
