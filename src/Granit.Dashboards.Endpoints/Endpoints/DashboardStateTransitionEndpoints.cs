using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for state transitions on the persisted <see cref="Dashboard"/>
/// aggregate — Publish (Draft → Published), Archive (any → Archived) and Restore
/// (Archived → Draft). Each handler returns the post-transition summary so the
/// caller can refresh its view without an extra GET.
/// </summary>
internal static class DashboardStateTransitionEndpoints
{
    public static RouteGroupBuilder MapStateTransitionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/publish", PublishAsync)
            .WithName("PublishDashboard")
            .WithSummary("Transitions the dashboard from Draft to Published.")
            .WithDescription(
                "Publishes a Draft dashboard, making it visible to consumers. "
                + "Idempotent — already-Published dashboards return the current state. "
                + "An Archived dashboard must be Restored before it can be Published "
                + "(returns 409 Conflict).")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/archive", ArchiveAsync)
            .WithName("ArchiveDashboard")
            .WithSummary("Archives the dashboard.")
            .WithDescription(
                "Archives the dashboard regardless of its current status. Idempotent — "
                + "already-Archived dashboards return the current state. Use POST "
                + "/{id}/restore to bring it back to Draft.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/restore", RestoreAsync)
            .WithName("RestoreDashboard")
            .WithSummary("Restores an archived dashboard to Draft.")
            .WithDescription(
                "Transitions an Archived dashboard back to Draft. Returns 409 Conflict "
                + "when the dashboard is not Archived (the domain rejects spurious "
                + "transitions to keep the state machine deterministic).")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static Task<Results<Ok<DashboardSummaryResponse>, ProblemHttpResult>> PublishAsync(
        [FromRoute] Guid id,
        [FromServices] DashboardStateTransitionService service,
        CancellationToken cancellationToken)
        => RunAsync(id, service.PublishAsync, cancellationToken);

    private static Task<Results<Ok<DashboardSummaryResponse>, ProblemHttpResult>> ArchiveAsync(
        [FromRoute] Guid id,
        [FromServices] DashboardStateTransitionService service,
        CancellationToken cancellationToken)
        => RunAsync(id, service.ArchiveAsync, cancellationToken);

    private static Task<Results<Ok<DashboardSummaryResponse>, ProblemHttpResult>> RestoreAsync(
        [FromRoute] Guid id,
        [FromServices] DashboardStateTransitionService service,
        CancellationToken cancellationToken)
        => RunAsync(id, service.RestoreAsync, cancellationToken);

    private static async Task<Results<Ok<DashboardSummaryResponse>, ProblemHttpResult>> RunAsync(
        Guid id,
        Func<Guid, CancellationToken, Task<DashboardStateTransitionResult>> transition,
        CancellationToken cancellationToken)
    {
        DashboardStateTransitionResult result = await transition(id, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            DashboardStateTransitionOutcome.Updated =>
                TypedResults.Ok(DashboardInstanceProjection.ToSummary(result.Dashboard!)),
            DashboardStateTransitionOutcome.NotFound =>
                TypedResults.Problem(
                    detail: $"Dashboard '{id}' not found.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardStateTransitionOutcome.Conflict =>
                TypedResults.Problem(
                    detail: result.ConflictReason,
                    statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unhandled outcome '{result.Outcome}'."),
        };
    }
}
