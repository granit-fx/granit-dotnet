using Granit.Dashboards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Write-side service that drives the <see cref="Dashboard"/> aggregate's state
/// machine — Publish (Draft → Published), Archive (any → Archived) and Restore
/// (Archived → Draft). Each transition loads the aggregate (multi-tenant filtered
/// by the DbContext), invokes the domain method, and persists the change.
/// </summary>
internal sealed class DashboardStateTransitionService(DashboardsDbContext db)
{
    public Task<DashboardStateTransitionResult> PublishAsync(Guid id, CancellationToken cancellationToken)
        => TransitionAsync(id, d => d.Publish(), cancellationToken);

    public Task<DashboardStateTransitionResult> ArchiveAsync(Guid id, CancellationToken cancellationToken)
        => TransitionAsync(id, d => d.Archive(), cancellationToken);

    public Task<DashboardStateTransitionResult> RestoreAsync(Guid id, CancellationToken cancellationToken)
        => TransitionAsync(id, d => d.Restore(), cancellationToken);

    private async Task<DashboardStateTransitionResult> TransitionAsync(
        Guid id,
        Action<Dashboard> transition,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await db.Dashboards
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardStateTransitionResult.NotFound();
        }

        try
        {
            transition(dashboard);
        }
        catch (InvalidOperationException ex)
        {
            return DashboardStateTransitionResult.Conflict(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DashboardStateTransitionResult.Success(dashboard);
    }
}

/// <summary>
/// Outcome of a state transition request — either the updated aggregate, a 404
/// (no row in the tenant scope), or a 409 with the domain's invariant message.
/// </summary>
internal sealed record DashboardStateTransitionResult(
    DashboardStateTransitionOutcome Outcome,
    Dashboard? Dashboard,
    string? ConflictReason)
{
    public static DashboardStateTransitionResult Success(Dashboard dashboard)
        => new(DashboardStateTransitionOutcome.Updated, dashboard, null);

    public static DashboardStateTransitionResult NotFound()
        => new(DashboardStateTransitionOutcome.NotFound, null, null);

    public static DashboardStateTransitionResult Conflict(string reason)
        => new(DashboardStateTransitionOutcome.Conflict, null, reason);
}

internal enum DashboardStateTransitionOutcome
{
    Updated,
    NotFound,
    Conflict,
}
