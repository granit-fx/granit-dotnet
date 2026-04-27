using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

internal sealed class EfLegalDocumentStore(
    IDbContextFactory<PrivacyDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : EfStoreBase<LegalDocument, PrivacyDbContext>(contextFactory, currentTenant),
      ILegalDocumentReader, ILegalDocumentWriter
{
    public Task<LegalDocument?> FindPublishedAsync(
        string documentId, CancellationToken cancellationToken = default) =>
        // IPublishable filter is active by default → only returns Published.
        FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);

    public Task<IReadOnlyList<LegalDocument>> GetAllPublishedAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<LegalDocument>(), cancellationToken);

    public async Task<LegalDocument?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Disable IPublishable filter to see drafts and archived versions.
        using IDisposable? _ = dataFilter?.Disable<IPublishable>();

        return await FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LegalDocument>> GetVersionHistoryAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = dataFilter?.Disable<IPublishable>();

        return await ListAsync(
            Spec.For<LegalDocument>()
                .Where(d => d.DocumentId == documentId)
                .OrderByDescending(d => d.Version),
            cancellationToken).ConfigureAwait(false);
    }

    Task ILegalDocumentWriter.InsertAsync(
        LegalDocument document, CancellationToken cancellationToken) =>
        base.AddAsync(document, cancellationToken);

    Task ILegalDocumentWriter.UpdateAsync(
        LegalDocument document, CancellationToken cancellationToken) =>
        base.UpdateAsync(document, cancellationToken);
}
