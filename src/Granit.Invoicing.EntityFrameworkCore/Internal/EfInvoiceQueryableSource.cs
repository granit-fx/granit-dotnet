using Granit.DataFiltering;
using Granit.Domain;
using Granit.Invoicing.Domain;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="Invoice"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// disabled so all invoices are returned cross-tenant.
/// </summary>
internal sealed class EfInvoiceQueryableSource(
    IDbContextFactory<InvoicingDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter) : IQueryableSource<Invoice>, IDisposable
{
    private readonly InvoicingDbContext _context = contextFactory.CreateDbContext();
    private readonly IDisposable? _tenantBypass = !currentTenant.IsAvailable
        ? dataFilter.Disable<IMultiTenant>()
        : null;

    public IQueryable<Invoice> GetQueryable() =>
        _context.Invoices.AsNoTracking();

    public void Dispose()
    {
        _tenantBypass?.Dispose();
        _context.Dispose();
    }
}
