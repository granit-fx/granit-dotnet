using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Internal;

/// <summary>
/// Default <see cref="ITenantQueryScope"/> — mirrors the CRUD-path guard in
/// <see cref="EfStoreBase{TEntity, TContext}.Query"/> exactly via the shared
/// <see cref="CrossTenantFilterDecision"/>.
/// </summary>
internal sealed class TenantQueryScope(
    ICurrentTenant currentTenant,
    ILogger<TenantQueryScope> logger,
    IHostAccessContext? hostAccess = null,
    PersistenceMetrics? metrics = null)
    : ITenantQueryScope
{
    /// <inheritdoc />
    public IQueryable<TEntity> Restrict<TEntity>(IQueryable<TEntity> query, string entityName)
        where TEntity : class
    {
        if (typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity))
            && !currentTenant.IsAvailable
            && CrossTenantFilterDecision.ShouldBypassMultiTenantFilter(
                entityName, hostAccess, metrics, logger))
        {
            return query.IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
        }

        return query;
    }
}
