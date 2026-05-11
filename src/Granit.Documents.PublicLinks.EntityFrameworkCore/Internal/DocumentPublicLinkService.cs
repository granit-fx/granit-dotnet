using Granit.BlobStorage;
using Granit.BlobStorage.Options;
using Granit.Documents.Domain;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.Events;
using Granit.Documents.PublicLinks.Options;
using Granit.Events;
using Granit.Guids;
using Granit.Users;
using Microsoft.Extensions.Options;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed <see cref="IDocumentPublicLinkService"/>. Composes
/// <see cref="IDocumentService"/> (to honour the parent tenant filter / trash
/// status at creation time) with <see cref="IDocumentPublicLinkStore"/>.
/// </summary>
/// <remarks>
/// Domain events raised by <see cref="DocumentPublicLink"/> are dispatched on
/// <c>SaveChanges</c> by <c>AuditedEntityInterceptor</c> / the persistence-side
/// dispatcher — no explicit event bus injection.
/// </remarks>
internal sealed class DocumentPublicLinkService(
    IDocumentPublicLinkStore store,
    IDocumentService documents,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider,
    IOptionsMonitor<GranitDocumentsPublicLinksOptions> optionsMonitor,
    IDistributedEventBus? distributedEventBus = null,
    ICurrentUserService? currentUser = null) : IDocumentPublicLinkService
{
    /// <inheritdoc/>
    public async Task<DocumentPublicLinkCreationResult> CreateAsync(
        Guid documentId,
        PublicLinkScope scope,
        TimeSpan ttl,
        int? maxUses,
        CancellationToken cancellationToken = default)
    {
        GranitDocumentsPublicLinksOptions options = optionsMonitor.CurrentValue;
        if (options.SigningKey.Length == 0)
        {
            throw new InvalidOperationException(
                "GranitDocumentsPublicLinksOptions.SigningKey is empty — public links cannot be minted. " +
                "Provision the HMAC pepper through Granit.Configuration.Vault before creating links.");
        }

        // 1. Tenant-filtered document fetch — proves the caller can see the doc.
        Document doc = await documents.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Document {documentId} was not found (or the current tenant cannot see it).");

        // 2. TTL handling: default fill-in + hard cap.
        TimeSpan effectiveTtl = ttl <= TimeSpan.Zero ? options.DefaultTtl : ttl;
        if (effectiveTtl > options.MaxTtl)
        {
            effectiveTtl = options.MaxTtl;
        }

        int? effectiveMaxUses = maxUses ?? options.DefaultMaxUses;

        // 3. Mint + hash.
        PublicLinkToken token = PublicLinkTokenFactory.GenerateRandom();
        byte[] tokenHash = PublicLinkTokenFactory.ComputeHash(token, options.SigningKey);

        DateTimeOffset now = timeProvider.GetUtcNow();
        var link = DocumentPublicLink.Create(
            guidGenerator.Create(),
            doc.Id,
            doc.TenantId,
            tokenHash,
            scope,
            now.Add(effectiveTtl),
            effectiveMaxUses,
            timeProvider);

        // 4. Persist (events flush on SaveChanges).
        await store.AddAsync(link, cancellationToken).ConfigureAwait(false);

        return new DocumentPublicLinkCreationResult(token, link);
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(Guid linkId, string? reason, CancellationToken cancellationToken = default)
    {
        DocumentPublicLink link = await store.FindByIdAsync(linkId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"DocumentPublicLink {linkId} was not found (or the current tenant cannot see it).");

        Guid? revokedBy = TryParseUserId(currentUser?.UserId);
        link.Revoke(revokedBy, reason, timeProvider);

        await store.UpdateAsync(link, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DocumentPublicLink>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default) =>
        store.ListForDocumentAsync(documentId, cancellationToken);

    /// <inheritdoc/>
    public async Task<DocumentPublicLink?> ResolveAndConsumeAsync(
        string token,
        string? clientIpMasked = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        GranitDocumentsPublicLinksOptions options = optionsMonitor.CurrentValue;
        if (options.SigningKey.Length == 0)
        {
            throw new InvalidOperationException(
                "GranitDocumentsPublicLinksOptions.SigningKey is empty — public links cannot be redeemed. " +
                "Provision the HMAC pepper through Granit.Configuration.Vault before serving redemptions.");
        }

        byte[] tokenHash = PublicLinkTokenFactory.ComputeHash(token, options.SigningKey);

        DocumentPublicLink? link = await store
            .ResolveByTokenHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!link.IsActive(now))
        {
            return null;
        }

        try
        {
            link.RegisterConsumption(timeProvider);
        }
        catch (InvalidOperationException)
        {
            // Defence in depth — a race with concurrent redemption/revocation has
            // already taken the aggregate out of the active set. 404 either way.
            return null;
        }

        await store.UpdateAsync(link, cancellationToken).ConfigureAwait(false);

        // F18.4 — publish the distributed audit event AFTER the consumption is durably
        // persisted (outbox semantics: never advertise a side-effect that isn't recorded).
        // Bus is optional — hosts without a distributed event provider get no-op behaviour.
        if (distributedEventBus is not null)
        {
            var eto = new DocumentPublicLinkConsumedEto(
                LinkId: link.Id,
                TenantId: link.TenantId,
                DocumentId: link.DocumentId,
                Scope: link.Scope,
                CurrentUses: link.CurrentUses,
                ConsumedAt: now,
                ClientIpMasked: clientIpMasked,
                UserAgent: userAgent);
            await distributedEventBus.PublishAsync(eto, cancellationToken).ConfigureAwait(false);
        }

        return link;
    }

    /// <inheritdoc/>
    public async Task<PresignedDownloadUrl?> CreateRedemptionUrlAsync(
        DocumentPublicLink link,
        bool forceAttachment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        // Force an attachment Content-Disposition for download scope; leave the
        // filename null for view scope so the browser may preview inline.
        DownloadUrlOptions? options = forceAttachment
            ? new DownloadUrlOptions(DownloadFileName: $"document-{link.DocumentId:N}")
            : null;

        return await documents
            .CreatePublicDownloadUrlAsync(link.DocumentId, options, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Guid? TryParseUserId(string? userId) =>
        Guid.TryParse(userId, out Guid parsed) ? parsed : null;
}
