using Granit.Querying;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.BackgroundJobs.Endpoints.Endpoints;

/// <summary>
/// GET endpoints for monitoring background job status.
/// </summary>
internal static class BackgroundJobsReadEndpoints
{
    /// <summary>
    /// Registers GET / and GET /{name} onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllJobsAsync)
            .WithName("GetAllBackgroundJobs")
            .WithSummary("Returns the current status of all registered background jobs with pagination.")
            .WithDescription("Lists all background jobs registered in the application with their current execution state, schedule, last run time, and next occurrence. Supports pagination via page and pageSize query parameters.");

        group.MapGet("/{name}", GetJobByNameAsync)
            .WithName("GetBackgroundJobByName")
            .WithSummary("Returns the status of a specific background job.")
            .WithDescription("Returns the detailed status of a single background job identified by its registered name. Includes execution state, last run time, next scheduled occurrence, and error information if the last run failed. Returns 404 if no job with the given name is registered.");

        return group;
    }

    private static async Task<Ok<PagedResult<BackgroundJobStatus>>> GetAllJobsAsync(
        [FromServices] IBackgroundJobReader reader,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QueryingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        int clampedPage = Math.Max(page, 1);
        int clampedPageSize = Math.Clamp(pageSize, 1, QueryingDefaults.MaxPageSize);

        IReadOnlyList<BackgroundJobStatus> all = await reader.GetAllAsync(cancellationToken).ConfigureAwait(false);

        int totalCount = all.Count;
        int skip = (clampedPage - 1) * clampedPageSize;
        var items = all.Skip(skip).Take(clampedPageSize).ToList();

        return TypedResults.Ok(new PagedResult<BackgroundJobStatus>(items, totalCount, HasMore: skip + items.Count < totalCount));
    }

    private static async Task<Results<Ok<BackgroundJobStatus>, NotFound>> GetJobByNameAsync(
        string name,
        IBackgroundJobReader reader,
        CancellationToken cancellationToken)
    {
        BackgroundJobStatus? job = await reader.FindAsync(name, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(job);
    }
}
