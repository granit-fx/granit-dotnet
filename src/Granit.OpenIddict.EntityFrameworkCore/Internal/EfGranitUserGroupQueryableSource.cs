using Granit.Identity.Local.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="GranitUserGroup"/>,
/// projecting from the consolidated <see cref="OpenIddictDbContext"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all user groups are returned cross-tenant.
/// </summary>
internal sealed class EfGranitUserGroupQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<GranitUserGroup>, IAsyncDisposable, IDisposable
{
    private OpenIddictDbContext? _context;

    public IQueryable<GranitUserGroup> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<GranitUserGroup> query = _context.UserGroups.AsNoTracking();
        return scope.Restrict(query, typeof(GranitUserGroup).Name);
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
