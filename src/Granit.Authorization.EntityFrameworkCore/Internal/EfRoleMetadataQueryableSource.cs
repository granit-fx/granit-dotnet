using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="RoleMetadata"/>. Host admins on a signaled host-access route see every row
/// (Host / Both / all tenants); tenant context applies the standard multi-tenant filter so
/// tenant admins only see their own tenant + the global Both rows. An unsignaled absent tenant
/// fails closed to the host partition.
/// </summary>
internal sealed class EfRoleMetadataQueryableSource<TContext>(
    TContext context,
    ITenantQueryScope scope) : IQueryableSource<RoleMetadata>
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    public IQueryable<RoleMetadata> GetQueryable() =>
        scope.Restrict(context.RoleMetadata.AsNoTracking(), typeof(RoleMetadata).Name);
}
