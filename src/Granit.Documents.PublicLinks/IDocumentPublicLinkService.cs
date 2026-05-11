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
}

/// <summary>
/// Result of <see cref="IDocumentPublicLinkService.CreateAsync"/>. Carries the
/// freshly minted raw token alongside the persisted aggregate.
/// </summary>
public sealed record DocumentPublicLinkCreationResult(
    PublicLinkToken Token,
    DocumentPublicLink Link);
