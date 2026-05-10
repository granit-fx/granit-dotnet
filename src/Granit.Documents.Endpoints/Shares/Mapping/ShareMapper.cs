using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Shares.Dtos;

namespace Granit.Documents.Endpoints.Shares.Mapping;

/// <summary>
/// Aggregate → wire-shape mapping for <see cref="DocumentShare"/>.
/// </summary>
internal static class ShareMapper
{
    public static ShareResponse ToResponse(this DocumentShare share)
    {
        ArgumentNullException.ThrowIfNull(share);
        return new ShareResponse(
            share.Id,
            share.TargetType,
            share.FolderId,
            share.DocumentId,
            share.GranteeType,
            share.GranteeId,
            share.Permission,
            share.IsDefault,
            share.ExpiresAt,
            share.CreatedAt,
            share.CreatedByUserId);
    }
}
