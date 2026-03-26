using Granit.QueryEngine;
using Granit.Workflow.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Workflow.Endpoints.Endpoints;

/// <summary>
/// GET endpoints for querying workflow status and transition history.
/// </summary>
internal static class WorkflowReadEndpoints
{
    /// <summary>
    /// Registers GET endpoints onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{entityType}/{entityId}/history", GetTransitionHistoryAsync)
            .WithName("GetWorkflowTransitionHistory")
            .WithSummary("Returns the ISO 27001-compliant audit trail of workflow transitions for an entity.")
            .WithDescription("Returns a paginated list of all state transitions for the specified entity, ordered by timestamp descending. Each entry includes the source and target state, the actor, an optional comment, and the transition timestamp. This history is immutable and serves as the ISO 27001 A.12.4 audit trail for workflow changes.")
            .Produces<PagedResult<WorkflowTransitionHistoryResponse>>();

        return group;
    }

    private static async Task<Ok<PagedResult<WorkflowTransitionHistoryResponse>>> GetTransitionHistoryAsync(
        string entityType,
        string entityId,
        [FromServices] IWorkflowHistoryQuery historyQuery,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        PagedResult<WorkflowTransitionHistoryResponse> result = await historyQuery.GetHistoryAsync(
            entityType, entityId, clampedPage, clampedPageSize, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }
}
