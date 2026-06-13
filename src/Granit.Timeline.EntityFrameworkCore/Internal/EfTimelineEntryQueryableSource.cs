using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="TimelineEntry"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all timeline entries are returned cross-tenant.
/// </summary>
internal sealed class EfTimelineEntryQueryableSource(
    IDbContextFactory<TimelineDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<TimelineEntry>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private TimelineDbContext? _context;

    public IQueryable<TimelineEntry> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<TimelineEntry> query = _context.TimelineEntries.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        TimelineDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
