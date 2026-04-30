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
/// HTTP handlers for the read side of the persisted <see cref="Dashboard"/>
/// aggregate — list (paged) and read-by-id. Multi-tenant filtering is applied
/// by <c>DashboardsDbContext</c> (via <c>ApplyGranitConventions</c>); these
/// handlers add status filtering, paging and DTO projection.
/// </summary>
internal static class DashboardInstanceEndpoints
{
    public static RouteGroupBuilder MapInstanceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListGranitDashboards")
            .WithSummary("Lists the current tenant's persisted dashboards (paged).")
            .WithDescription(
                "Returns the tenant's dashboards as a paged summary list, optionally "
                + "filtered by ?status= (Draft / Published / Archived). Results are "
                + "ordered by Name ascending. Pagination defaults: page=0, pageSize=50; "
                + "pageSize is capped at 200. Multi-tenant filter is applied by the "
                + "DbContext — cross-tenant rows are never reachable.")
            .RequireAuthorization(DashboardsPermissions.Instances.Read)
            .Produces<PagedResponse<DashboardSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", ReadByIdAsync)
            .WithName("ReadGranitDashboardById")
            .WithSummary("Reads a single persisted dashboard with its widget tree.")
            .WithDescription(
                "Returns the full dashboard payload including the layout values and "
                + "the ordered widget pool. Multi-tenant filtered — returns 404 when "
                + "the id is not found in the current tenant's scope (avoids leaking "
                + "the existence of cross-tenant dashboards).")
            .RequireAuthorization(DashboardsPermissions.Instances.Read)
            .Produces<DashboardDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<PagedResponse<DashboardSummaryResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] DashboardReader reader,
        [FromQuery] DashboardStatus? status,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DashboardListPage result = await reader.ListAsync(status, page, pageSize, cancellationToken).ConfigureAwait(false);

            PagedResponse<DashboardSummaryResponse> response = new(
                Items: [.. result.Items.Select(DashboardInstanceProjection.ToSummary)],
                TotalCount: result.TotalCount,
                Page: result.Page,
                PageSize: result.PageSize);

            return TypedResults.Ok(response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<Ok<DashboardDetailResponse>, ProblemHttpResult>> ReadByIdAsync(
        [FromRoute] Guid id,
        [FromServices] DashboardReader reader,
        CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await reader.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (dashboard is null)
        {
            return TypedResults.Problem(
                detail: $"Dashboard '{id}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(DashboardInstanceProjection.ToDetail(dashboard));
    }
}
