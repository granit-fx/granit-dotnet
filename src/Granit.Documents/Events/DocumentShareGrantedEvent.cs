using Granit.Documents.Domain;
using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <see cref="DocumentShare"/> is granted on a folder or a document.
/// </summary>
/// <remarks>
/// Consumed by the F6.3 cache layer to invalidate the matching ACL tag
/// (<c>acl:{tenantId}:folder:{folderId}</c> or <c>acl:{tenantId}:doc:{documentId}</c>).
/// </remarks>
public sealed record DocumentShareGrantedEvent(
    Guid ShareId,
    Guid? TenantId,
    ShareTargetType TargetType,
    Guid? FolderId,
    Guid? DocumentId,
    ShareGranteeType GranteeType,
    Guid GranteeId,
    SharePermissionLevel Permission,
    bool IsDefault,
    DateTimeOffset? ExpiresAt) : IDomainEvent;
