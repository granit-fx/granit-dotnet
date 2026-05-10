using Granit.Documents.Domain;
using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <see cref="DocumentShare"/> is revoked.
/// </summary>
/// <remarks>
/// Consumed by the F6.3 cache layer to invalidate the matching ACL tag.
/// </remarks>
public sealed record DocumentShareRevokedEvent(
    Guid ShareId,
    Guid? TenantId,
    ShareTargetType TargetType,
    Guid? FolderId,
    Guid? DocumentId,
    ShareGranteeType GranteeType,
    Guid GranteeId) : IDomainEvent;
