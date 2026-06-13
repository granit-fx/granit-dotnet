using Granit.MultiTenancy;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="GranitOpenIddictApplication"/>, backing <c>MapGranitQuery</c> and the analytics
/// runner over <c>ApplicationQuery</c>. Projects from the OpenIddict-managed entity set on the
/// consolidated <see cref="OpenIddictDbContext"/> (registered via <c>ReplaceDefaultEntities</c>).
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed.
/// </summary>
internal sealed class EfGranitOpenIddictApplicationQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<GranitOpenIddictApplication>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private OpenIddictDbContext? _context;

    public IQueryable<GranitOpenIddictApplication> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<GranitOpenIddictApplication> query =
            _context.Set<GranitOpenIddictApplication>().AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        OpenIddictDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
