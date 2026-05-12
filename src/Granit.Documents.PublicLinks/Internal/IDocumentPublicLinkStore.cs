using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks.Internal;

/// <summary>Persistence surface for <see cref="DocumentPublicLink"/> rows.</summary>
internal interface IDocumentPublicLinkStore
{
    /// <summary>
    /// Resolves a link by its HMAC digest, bypassing tenant filters — the
    /// anonymous redemption endpoint has no tenant context; the link carries
    /// its own <c>TenantId</c>.
    /// </summary>
    Task<DocumentPublicLink?> ResolveByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Lists every link issued against <paramref name="documentId"/>, tenant-filtered.</summary>
    Task<IReadOnlyList<DocumentPublicLink>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Loads a single link by id, tenant-filtered.</summary>
    Task<DocumentPublicLink?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new link (raises domain events via the configured interceptor on save).</summary>
    Task AddAsync(DocumentPublicLink link, CancellationToken cancellationToken = default);

    /// <summary>Saves pending updates against the tracked link.</summary>
    Task UpdateAsync(DocumentPublicLink link, CancellationToken cancellationToken = default);
}
