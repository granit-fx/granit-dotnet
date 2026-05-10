using Granit.Documents.Domain;

namespace Granit.Documents;

/// <summary>
/// Domain orchestration contract for ACL share grant / revoke / listing (F6.1).
/// The HTTP layer (<c>Granit.Documents.Endpoints</c>) consumes this interface; the EF Core
/// implementation lives in <c>Granit.Documents.EntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// Effective-permission resolution (F6.2) and FusionCache invalidation (F6.3) are layered
/// on top in subsequent stories — F6.1 is limited to write/read of the share rows.
/// </remarks>
public interface IDocumentShareService
{
    /// <summary>
    /// Grants <paramref name="permission"/> on the folder identified by
    /// <paramref name="folderId"/> to (<paramref name="granteeType"/>, <paramref name="granteeId"/>).
    /// </summary>
    /// <returns>The created <see cref="DocumentShare"/>, or <c>null</c> when the folder does not exist.</returns>
    /// <exception cref="ArgumentException">When inputs are invalid (per the aggregate factory).</exception>
    Task<DocumentShare?> GrantOnFolderAsync(
        Guid folderId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        bool isDefault,
        Guid createdByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants <paramref name="permission"/> on the document identified by
    /// <paramref name="documentId"/> to (<paramref name="granteeType"/>, <paramref name="granteeId"/>).
    /// </summary>
    /// <returns>The created <see cref="DocumentShare"/>, or <c>null</c> when the document does not exist.</returns>
    Task<DocumentShare?> GrantOnDocumentAsync(
        Guid documentId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        Guid createdByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the share with the given identifier.
    /// </summary>
    /// <returns><c>true</c> when a row was deleted; <c>false</c> when the share does not exist.</returns>
    Task<bool> RevokeAsync(Guid shareId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active shares granted on the folder identified by <paramref name="folderId"/>,
    /// ordered by creation time. Expired shares are excluded.
    /// </summary>
    Task<IReadOnlyList<DocumentShare>> ListForFolderAsync(
        Guid folderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active shares granted on the document identified by <paramref name="documentId"/>,
    /// ordered by creation time. Expired shares are excluded.
    /// </summary>
    Task<IReadOnlyList<DocumentShare>> ListForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
