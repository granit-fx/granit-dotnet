using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataExchange.Endpoints.Endpoints.Import;

/// <summary>
/// Import job listing endpoint for admin views.
/// </summary>
internal static class ImportJobListEndpoints
{
    /// <summary>
    /// Registers GET /jobs onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapImportJobListEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/jobs", ListAsync)
            .WithName("ListImportJobs")
            .WithSummary("Lists import jobs with optional status filter and pagination.")
            .WithDescription("Returns a paginated list of import jobs ordered by creation date descending. Supports filtering by job status. Intended for admin dashboards monitoring import activity.")
            .Produces<PagedResult<ImportJobResponse>>();

        return group;
    }

    private static async Task<Ok<PagedResult<ImportJobResponse>>> ListAsync(
        [FromServices] IImportJobReader jobReader,
        [FromQuery] ImportJobStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        PagedResult<ImportJob> result = await jobReader
            .ListAsync(status, clampedPage, clampedPageSize, cancellationToken)
            .ConfigureAwait(false);

        PagedResult<ImportJobResponse> response = new(
            result.Items.Select(ImportJobResponse.FromJob).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(response);
    }
}
