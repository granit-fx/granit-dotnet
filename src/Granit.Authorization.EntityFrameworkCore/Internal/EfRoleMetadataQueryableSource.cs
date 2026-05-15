using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="RoleMetadata"/>. Host admins see every row (Host / Both / all tenants);
/// tenant context applies the standard multi-tenant filter so tenant admins only
/// see their own tenant + the global Both rows.
/// </summary>
internal sealed class EfRoleMetadataQueryableSource<TContext>(
    TContext context,
    ICurrentTenant currentTenant) : IQueryableSource<RoleMetadata>
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    public IQueryable<RoleMetadata> GetQueryable()
    {
        IQueryable<RoleMetadata> query = context.RoleMetadata.AsNoTracking();
        return currentTenant.IsAvailable
            ? query
            : query.IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
    }
}
