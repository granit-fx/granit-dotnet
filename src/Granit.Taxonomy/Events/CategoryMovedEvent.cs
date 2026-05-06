using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a category's parent changes.</summary>
public sealed record CategoryMovedEvent(
    Guid CategoryId,
    Guid? OldParentId,
    Guid? NewParentId) : IDomainEvent;
