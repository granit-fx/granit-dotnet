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
    ICurrentTenant currentTenant) : IQueryableSource<ApiKeyEntry>
{
    private readonly AuthenticationApiKeysDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<ApiKeyEntry> GetQueryable()
    {
        IQueryable<ApiKeyEntry> query = _context.ApiKeys.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
