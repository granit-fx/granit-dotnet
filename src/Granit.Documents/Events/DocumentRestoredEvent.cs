using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a trashed <c>Document</c> is restored to active status.
/// </summary>
public sealed record DocumentRestoredEvent(
    Guid DocumentId,
    Guid? TenantId) : IDomainEvent;
