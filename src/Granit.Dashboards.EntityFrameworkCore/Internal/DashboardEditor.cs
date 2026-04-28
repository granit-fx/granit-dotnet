using Granit.Dashboards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Write-side service for editable <see cref="Dashboard"/> metadata — name and
/// grid layout. Each call is a single load + mutate + save round-trip and
/// inherits the multi-tenant filter from <see cref="DashboardsDbContext"/>.
/// </summary>
internal sealed class DashboardEditor(DashboardsDbContext db)
{
    public async Task<DashboardEditResult> UpdateMetadataAsync(
        Guid id,
        string name,
        int layoutColumns,
        int layoutRowHeight,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await db.Dashboards
            .Include(d => d.Widgets)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardEditResult.NotFound();
        }

        try
        {
            dashboard.Rename(name);
            dashboard.UpdateLayout(layoutColumns, layoutRowHeight);
        }
        catch (ArgumentException ex)
        {
            return DashboardEditResult.Invalid(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DashboardEditResult.Success(dashboard);
    }
}

/// <summary>
/// Outcome of an edit request — updated aggregate, 404 (no row in scope), or
/// 422 carrying the domain-guard message (defense-in-depth — FluentValidation
/// runs first at the HTTP layer).
/// </summary>
internal sealed record DashboardEditResult(
    DashboardEditOutcome Outcome,
    Dashboard? Dashboard,
    string? InvalidReason)
{
    public static DashboardEditResult Success(Dashboard dashboard)
        => new(DashboardEditOutcome.Updated, dashboard, null);

    public static DashboardEditResult NotFound()
        => new(DashboardEditOutcome.NotFound, null, null);

    public static DashboardEditResult Invalid(string reason)
        => new(DashboardEditOutcome.Invalid, null, reason);
}

internal enum DashboardEditOutcome
{
    Updated,
    NotFound,
    Invalid,
}
