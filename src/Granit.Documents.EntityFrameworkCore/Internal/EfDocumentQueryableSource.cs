using Granit.DataFiltering;
using Granit.Documents.Domain;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="Document"/>. Powers the generic <c>/documents/query</c> endpoint
/// wired by <c>Granit.Documents.Endpoints</c>. When no tenant context is active
/// (host admin), the multi-tenant query filter is disabled so all documents are
/// returned cross-tenant.
/// </summary>
internal sealed class EfDocumentQueryableSource(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter) : IQueryableSource<Document>, IDisposable
{
    private readonly DocumentsDbContext _context = contextFactory.CreateDbContext();
    private readonly IDisposable? _tenantBypass = !currentTenant.IsAvailable
        ? dataFilter.Disable<IMultiTenant>()
        : null;

    public IQueryable<Document> GetQueryable() =>
        _context.Documents.AsNoTracking();

    public void Dispose()
    {
        _tenantBypass?.Dispose();
        _context.Dispose();
    }
}
