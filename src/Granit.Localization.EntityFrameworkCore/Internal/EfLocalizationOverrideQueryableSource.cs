using Granit.Localization.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Localization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="LocalizationOverride"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all overrides are returned cross-tenant — required for ISO 27001
/// cross-tenant translation governance.
/// </summary>
internal sealed class EfLocalizationOverrideQueryableSource(
    IDbContextFactory<LocalizationDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<LocalizationOverride>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private LocalizationDbContext? _context;

    public IQueryable<LocalizationOverride> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<LocalizationOverride> query = _context.LocalizationOverrides.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        LocalizationDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
