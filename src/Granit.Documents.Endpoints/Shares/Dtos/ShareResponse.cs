using Granit.Documents.Domain;

namespace Granit.Documents.Endpoints.Shares.Dtos;

/// <summary>
/// Wire-shape response for a single <see cref="DocumentShare"/>.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one of <see cref="FolderId"/> / <see cref="DocumentId"/> is non-null, matching
/// <see cref="TargetType"/>; the others stay <c>null</c> so OpenAPI consumers can branch on
/// the discriminator without fishing through nullables.
/// </para>
/// </remarks>
public sealed record ShareResponse(
    Guid Id,
    ShareTargetType TargetType,
    Guid? FolderId,
    Guid? DocumentId,
    ShareGranteeType GranteeType,
    Guid GranteeId,
    SharePermissionLevel Permission,
    bool IsDefault,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId);
