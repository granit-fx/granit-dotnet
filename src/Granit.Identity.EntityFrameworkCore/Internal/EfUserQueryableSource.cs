using Granit.Identity.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="User"/>,
/// backing <c>MapGranitQuery&lt;User&gt;</c> and the analytics runner over <c>UserQuery</c>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all users are returned cross-tenant.
/// </summary>
/// <remarks>
/// Distinct from <see cref="EfUserDirectoryQueryableSource"/> (which serves the
/// <see cref="IUserDirectoryQueryableSource"/> directory contract): the query engine
/// resolves the open generic <see cref="IQueryableSource{User}"/>, so it needs its own
/// registration even though both project the same <see cref="IdentityDbContext.Users"/> set.
/// </remarks>
internal sealed class EfUserQueryableSource(
    IDbContextFactory<IdentityDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<User>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private IdentityDbContext? _context;

    public IQueryable<User> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<User> query = _context.Users.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        IdentityDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
