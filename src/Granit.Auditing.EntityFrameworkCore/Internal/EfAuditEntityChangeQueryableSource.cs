using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AuditEntityChange"/>.
/// Joins through the parent <see cref="AuditEntry"/>'s tenant filter — when no tenant context
/// is active (host admin), the multi-tenant filter on the parent is bypassed so all entity
/// changes are returned cross-tenant.
/// </summary>
internal sealed class EfAuditEntityChangeQueryableSource(
    IDbContextFactory<AuditingDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<AuditEntityChange>
{
    private readonly AuditingDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<AuditEntityChange> GetQueryable()
    {
        IQueryable<AuditEntityChange> query = _context.AuditEntityChanges.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
