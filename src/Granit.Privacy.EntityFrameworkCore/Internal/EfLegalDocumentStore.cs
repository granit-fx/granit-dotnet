using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
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
    private readonly IDbContextFactory<PrivacyDbContext> _contextFactory = contextFactory;

    public async Task<LegalDocument?> FindPublishedAsync(
        string documentId, CancellationToken cancellationToken = default)
    {
        // IPublishable filter is active by default → only returns Published.
        await using PrivacyDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.LegalDocuments
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LegalDocument>> GetAllPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        await using PrivacyDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.LegalDocuments
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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

        await using PrivacyDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.LegalDocuments
            .Where(d => d.DocumentId == documentId)
            .OrderByDescending(d => d.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    Task ILegalDocumentWriter.InsertAsync(
        LegalDocument document, CancellationToken cancellationToken) =>
        base.AddAsync(document, cancellationToken);

    Task ILegalDocumentWriter.UpdateAsync(
        LegalDocument document, CancellationToken cancellationToken) =>
        base.UpdateAsync(document, cancellationToken);
}
