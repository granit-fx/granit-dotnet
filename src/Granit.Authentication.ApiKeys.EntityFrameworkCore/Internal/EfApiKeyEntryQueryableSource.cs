using Granit.Authentication.ApiKeys.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ApiKeyEntry"/>,
/// backing <c>MapGranitQuery&lt;ApiKeyEntry&gt;</c>. When no tenant context is active (host admin
/// reaching the endpoint through <c>.AllowHostAccess()</c>), the multi-tenant query filter is
/// bypassed so keys are returned cross-tenant; otherwise the standard tenant scope applies.
/// </summary>
internal sealed class EfApiKeyEntryQueryableSource(
    IDbContextFactory<AuthenticationApiKeysDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<ApiKeyEntry>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private AuthenticationApiKeysDbContext? _context;

    public IQueryable<ApiKeyEntry> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ApiKeyEntry> query = _context.ApiKeys.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        AuthenticationApiKeysDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
