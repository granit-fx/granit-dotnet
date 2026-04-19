using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AuditEntry"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed
/// so all audit entries are returned cross-tenant — required for ISO 27001 audit trail review.
/// </summary>
internal sealed class EfAuditEntryQueryableSource(
    IDbContextFactory<AuditingDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<AuditEntry>
{
    private readonly AuditingDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<AuditEntry> GetQueryable()
    {
        IQueryable<AuditEntry> query = _context.AuditEntries
            .Include(e => e.EntityChanges)
            .AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
