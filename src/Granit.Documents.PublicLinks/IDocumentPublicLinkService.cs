using Granit.BlobStorage;
using Granit.BlobStorage.Options;
using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks;

/// <summary>
/// Application-level surface for managing <see cref="DocumentPublicLink"/> records.
/// Concrete implementation lives in <c>Granit.Documents.PublicLinks.EntityFrameworkCore</c>
/// (F18.2).
/// </summary>
public interface IDocumentPublicLinkService
{
    /// <summary>
    /// Mints a fresh public link for <paramref name="documentId"/>. The returned
    /// <see cref="DocumentPublicLinkCreationResult.Token"/> is the raw token —
    /// returned exactly once and never persisted in cleartext.
    /// </summary>
    Task<DocumentPublicLinkCreationResult> CreateAsync(
        Guid documentId,
        PublicLinkScope scope,
        TimeSpan ttl,
        int? maxUses,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes the link identified by <paramref name="linkId"/>.</summary>
    Task RevokeAsync(Guid linkId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Lists every public link issued against <paramref name="documentId"/>, newest first.</summary>
    Task<IReadOnlyList<DocumentPublicLink>> ListForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymous redemption path (F18.3). Hashes <paramref name="token"/> with the host
    /// signing key, resolves the matching link bypassing the tenant filter, validates
    /// it (not revoked, not expired, MaxUses not exhausted) and atomically increments
    /// <see cref="DocumentPublicLink.CurrentUses"/>. Returns the consumed aggregate on
    /// success, or <c>null</c> on any business validation failure — callers MUST treat
    /// <c>null</c> as 404 (no info disclosure).
    /// </summary>
    Task<DocumentPublicLink?> ResolveAndConsumeAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a short-lived presigned download URL for the document targeted by
    /// <paramref name="link"/>, bypassing the tenant filter. Returns <c>null</c> when
    /// the underlying document has been trashed / permanently-deleted, or no current
    /// version exists. Callers MUST treat <c>null</c> as 404 (no info disclosure).
    /// </summary>
    /// <param name="link">A link previously returned by <see cref="ResolveAndConsumeAsync"/>.</param>
    /// <param name="forceAttachment">
    /// When <c>true</c> the URL carries a <c>Content-Disposition: attachment</c> header
    /// (the bearer is forced to download); when <c>false</c> the URL preserves the
    /// storage default (inline preview where the browser can).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PresignedDownloadUrl?> CreateRedemptionUrlAsync(
        DocumentPublicLink link,
        bool forceAttachment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of <see cref="IDocumentPublicLinkService.CreateAsync"/>. Carries the
/// freshly minted raw token alongside the persisted aggregate.
/// </summary>
public sealed record DocumentPublicLinkCreationResult(
    PublicLinkToken Token,
    DocumentPublicLink Link);
