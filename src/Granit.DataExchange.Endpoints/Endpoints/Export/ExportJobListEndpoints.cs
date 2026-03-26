using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataExchange.Endpoints.Endpoints.Export;

/// <summary>
/// Export job listing endpoint for admin views.
/// </summary>
internal static class ExportJobListEndpoints
{
    /// <summary>
    /// Registers GET /jobs onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapExportJobListEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/jobs", ListAsync)
            .WithName("ListExportJobs")
            .WithSummary("Lists export jobs with optional status filter and pagination.")
            .WithDescription("Returns a paginated list of export jobs ordered by creation date descending. Supports filtering by job status (Created, Processing, Completed, Failed). Intended for admin dashboards monitoring export activity.")
            .Produces<PagedResult<ExportJobResponse>>();

        return group;
    }

    private static async Task<Ok<PagedResult<ExportJobResponse>>> ListAsync(
        [FromServices] IExportJobReader jobReader,
        [FromQuery] ExportJobStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        PagedResult<ExportJob> result = await jobReader
            .ListAsync(status, clampedPage, clampedPageSize, cancellationToken)
            .ConfigureAwait(false);

        PagedResult<ExportJobResponse> response = new(
            result.Items.Select(ExportJobResponse.FromJob).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(response);
    }
}
