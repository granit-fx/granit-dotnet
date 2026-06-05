using Granit.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace Granit.BackgroundJobs.Endpoints.Endpoints;

/// <summary>
/// POST endpoints for controlling background job execution.
/// </summary>
internal static class BackgroundJobsWriteEndpoints
{
    /// <summary>
    /// Registers POST /{name}/pause, POST /{name}/resume, POST /{name}/trigger
    /// onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{name}/pause", PauseJobAsync)
            .WithName("PauseBackgroundJob")
            .WithSummary("Pauses a recurring background job. The current execution completes normally.")
            .WithDescription("Pauses the job's recurring schedule. If the job is currently executing, the in-flight execution will complete — only future occurrences are suppressed. Use the resume endpoint to re-enable scheduling. Returns 404 if the job name is not registered.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddOpenApiOperationTransformer(DescribeNameParam);

        group.MapPost("/{name}/resume", ResumeJobAsync)
            .WithName("ResumeBackgroundJob")
            .WithSummary("Resumes a paused background job and schedules its next occurrence.")
            .WithDescription("Re-enables the recurring schedule of a previously paused job and computes the next occurrence. No-op if the job is not paused. Returns 404 if the job name is not registered.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddOpenApiOperationTransformer(DescribeNameParam);

        group.MapPost("/{name}/trigger", TriggerJobAsync)
            .WithName("TriggerBackgroundJob")
            .WithSummary("Triggers an immediate execution of the job, independent of its schedule.")
            .WithDescription("Enqueues the job for immediate execution regardless of its cron schedule or paused state. The response is 202 Accepted — the actual execution is asynchronous. Does not affect the regular schedule. Returns 404 if the job name is not registered.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddOpenApiOperationTransformer(DescribeNameParam);

        return group;
    }

    private static Task DescribeNameParam(
        OpenApiOperation op,
        OpenApiOperationTransformerContext _,
        CancellationToken __)
    {
        IOpenApiParameter? param = op.Parameters?.FirstOrDefault(p => p.Name == "name");
        if (param is not null)
        {
            param.Description = "Unique registered name of the background job.";
        }
        return Task.CompletedTask;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PauseJobAsync(
        string name,
        [FromServices] IBackgroundJobWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.PauseAsync(name, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (EntityNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResumeJobAsync(
        string name,
        [FromServices] IBackgroundJobWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.ResumeAsync(name, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (EntityNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> TriggerJobAsync(
        string name,
        [FromServices] IBackgroundJobWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.TriggerNowAsync(name, cancellationToken).ConfigureAwait(false);
            return TypedResults.Accepted((string?)null);
        }
        catch (EntityNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
    }
}
