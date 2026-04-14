using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.ReferenceData.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for reference data entities.
/// Applies scope-aware tenant filtering identical to <see cref="EfCoreReferenceDataStore{TEntity,TDbContext}"/>.
/// </summary>
internal sealed class ReferenceDataQueryableSource<TEntity, TDbContext>(
    IDbContextFactory<TDbContext> contextFactory,
    ReferenceDataScope scope) : IQueryableSource<TEntity>
    where TEntity : ReferenceDataEntity
    where TDbContext : DbContext
{
    private readonly TDbContext _context = contextFactory.CreateDbContext();

    /// <inheritdoc/>
    public IQueryable<TEntity> GetQueryable()
    {
        IQueryable<TEntity> queryable = _context.Set<TEntity>().AsNoTracking();

        if (scope == ReferenceDataScope.Global)
        {
            queryable = queryable
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.TenantId == null);
        }

        return queryable;
    }
}
