using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="PermissionGrant"/>. On a signaled host-access route with no active tenant the
/// multi-tenant query filter is bypassed so all grants are returned cross-tenant — required for
/// ISO 27001 cross-tenant authorization review. An unsignaled absent tenant fails closed to the
/// host partition.
/// </summary>
internal sealed class EfPermissionGrantQueryableSource<TContext>(
    TContext context,
    ITenantQueryScope scope) : IQueryableSource<PermissionGrant>
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    public IQueryable<PermissionGrant> GetQueryable() =>
        scope.Restrict(context.PermissionGrants.AsNoTracking(), typeof(PermissionGrant).Name);
}
