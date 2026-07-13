using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.Privacy.LegalAgreements.Events;
using Granit.Privacy.LegalAgreements.Exceptions;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

/// <summary>
/// Publishes a legal document draft. Archives the previous published version in the same
/// transaction and dispatches <see cref="LegalAgreementObsoleteEto"/> for re-consent flows.
/// </summary>
internal sealed class LegalDocumentPublicationService(
    IDbContextFactory<PrivacyDbContext> contextFactory,
    IDistributedEventBus eventBus,
    IDataFilter? dataFilter = null) : ILegalDocumentPublicationService
{
    public async Task<LegalDocument> PublishAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        // Disable IPublishable filter to see both drafts and currently published.
        using IDisposable? _ = dataFilter?.Disable<IPublishable>();

        await using PrivacyDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        LegalDocument draft = await db.LegalDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new LegalDocumentNotFoundException(documentId);

        if (draft.LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new LegalDocumentNotPublishableException(documentId, draft.LifecycleStatus);
        }

        // Find the currently published version for the same DocumentId.
        LegalDocument? currentPublished = await db.LegalDocuments
            .FirstOrDefaultAsync(
                d => d.DocumentId == draft.DocumentId
                    && d.IsPublished
                    && d.Id != draft.Id,
                cancellationToken)
            .ConfigureAwait(false);

        // Archive the old version (dispatches LegalAgreementObsoleteEto via domain event).
        currentPublished?.Archive(draft.Version.ToString());

        // Publish the new version.
        draft.Publish();

        // Save both changes atomically — the unique filtered index ensures
        // at most one Published version per (TenantId, DocumentId).
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Dispatch cache invalidation so all pods refresh their registry.
        await eventBus.PublishAsync(
            new LegalDocumentCacheInvalidatedEto(draft.DocumentId),
            cancellationToken).ConfigureAwait(false);

        return draft;
    }
}
