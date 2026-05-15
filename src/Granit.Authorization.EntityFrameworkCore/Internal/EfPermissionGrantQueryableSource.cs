using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="PermissionGrant"/>. When no tenant context is active (host admin)
/// the multi-tenant query filter is bypassed so all grants are returned cross-tenant
/// — required for ISO 27001 cross-tenant authorization review.
/// </summary>
internal sealed class EfPermissionGrantQueryableSource<TContext>(
    TContext context,
    ICurrentTenant currentTenant) : IQueryableSource<PermissionGrant>
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    public IQueryable<PermissionGrant> GetQueryable()
    {
        IQueryable<PermissionGrant> query = context.PermissionGrants.AsNoTracking();
        return currentTenant.IsAvailable
            ? query
            : query.IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
    }
}
