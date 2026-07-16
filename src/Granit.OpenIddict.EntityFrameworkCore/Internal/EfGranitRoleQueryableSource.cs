using Granit.Identity.Local.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="GranitRole"/>,
/// backing <c>MapGranitQuery&lt;GranitRole&gt;</c> and the analytics runner over <c>GranitRoleQuery</c>.
/// Projects from the inherited ASP.NET Identity <c>Roles</c> set owned by the consolidated
/// <see cref="OpenIddictDbContext"/>.
/// </summary>
/// <remarks>
/// <see cref="GranitRole"/> is not <c>IMultiTenant</c> (roles are host-global), so no
/// query-filter bypass is required.
/// </remarks>
internal sealed class EfGranitRoleQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory)
    : IQueryableSource<GranitRole>, IAsyncDisposable, IDisposable
{
    private OpenIddictDbContext? _context;

    public IQueryable<GranitRole> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        return _context.Set<GranitRole>().AsNoTracking();
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
