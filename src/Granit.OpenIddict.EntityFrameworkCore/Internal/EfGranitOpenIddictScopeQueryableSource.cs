using Granit.OpenIddict.Entities.OpenIddict;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="GranitOpenIddictScope"/>, backing <c>MapGranitQuery</c> and the analytics runner
/// over <c>ScopeQuery</c>. Projects from the OpenIddict-managed entity set on the consolidated
/// <see cref="OpenIddictDbContext"/> (registered via <c>ReplaceDefaultEntities</c>).
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed.
/// </summary>
internal sealed class EfGranitOpenIddictScopeQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<GranitOpenIddictScope>, IAsyncDisposable, IDisposable
{
    private OpenIddictDbContext? _context;

    public IQueryable<GranitOpenIddictScope> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<GranitOpenIddictScope> query =
            _context.Set<GranitOpenIddictScope>().AsNoTracking();
        return scope.Restrict(query, typeof(GranitOpenIddictScope).Name);
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
