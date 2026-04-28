using Granit.Dashboards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Read-side service for the persisted <see cref="Dashboard"/> aggregate. The
/// <see cref="DashboardsDbContext"/> already applies the multi-tenant query filter
/// via <c>ApplyGranitConventions</c>, so the reader only handles status / paging
/// and widget eager-loading.
/// </summary>
internal sealed class DashboardReader(DashboardsDbContext db)
{
    /// <summary>
    /// Lists the current tenant's dashboards, optionally filtered by status, with
    /// stable ordering (Name ascending). Returns the page slice plus the total
    /// count so the caller can render pagination controls.
    /// </summary>
    public async Task<DashboardListPage> ListAsync(
        DashboardStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 0.");
        }

        if (pageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
        }

        IQueryable<Dashboard> query = db.Dashboards.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<Dashboard> items = await query
            .OrderBy(d => d.Name)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardListPage(items, total, page, pageSize);
    }

    /// <summary>
    /// Returns the dashboard with the supplied id (eager-loading widgets) or
    /// <c>null</c> when none exists. Multi-tenant filtered through the DbContext.
    /// </summary>
    public Task<Dashboard?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => db.Dashboards
            .AsNoTracking()
            .Include(d => d.Widgets)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
}

/// <summary>Pagination envelope returned by <see cref="DashboardReader.ListAsync"/>.</summary>
internal sealed record DashboardListPage(
    IReadOnlyList<Dashboard> Items,
    int TotalCount,
    int Page,
    int PageSize);
