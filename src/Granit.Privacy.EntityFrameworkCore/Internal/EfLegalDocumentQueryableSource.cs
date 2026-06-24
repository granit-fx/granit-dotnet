using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="LegalDocument"/>,
/// backing <c>MapGranitQuery&lt;LegalDocument&gt;</c> on the admin endpoints.
/// </summary>
/// <remarks>
/// The <c>Publishable</c> global query filter is always bypassed so drafts and archived
/// versions are visible alongside published ones. The multi-tenant filter is also bypassed
/// when no tenant context is active (host-level admin), returning documents across all tenants.
/// </remarks>
internal sealed class EfLegalDocumentQueryableSource(
    IDbContextFactory<PrivacyDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<LegalDocument>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private PrivacyDbContext? _context;

    public IQueryable<LegalDocument> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<LegalDocument> query = _context.LegalDocuments.AsNoTracking();

        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant, GranitFilterNames.Publishable])
            : query.IgnoreQueryFilters([GranitFilterNames.Publishable]);
    }

    public ValueTask DisposeAsync()
    {
        PrivacyDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
