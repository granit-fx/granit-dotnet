using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ScheduledAction"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all scheduled actions are returned cross-tenant.
/// </summary>
internal sealed class EfScheduledActionQueryableSource(
    IDbContextFactory<SchedulingDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<ScheduledAction>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private SchedulingDbContext? _context;

    public IQueryable<ScheduledAction> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ScheduledAction> query = _context.ScheduledActions.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        SchedulingDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
