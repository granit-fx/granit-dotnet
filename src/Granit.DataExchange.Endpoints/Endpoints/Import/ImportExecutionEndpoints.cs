using Granit.Commands;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataExchange.Endpoints.Endpoints.Import;

/// <summary>
/// Execution, dry-run, status, and cancellation endpoints (Story #497).
/// </summary>
internal static class ImportExecutionEndpoints
{
    /// <summary>
    /// Registers POST /{jobId}/execute, POST /{jobId}/dry-run, GET /{jobId},
    /// DELETE /{jobId} onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapExecutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{jobId:guid}/execute", ExecuteAsync)
            .WithName("ExecuteImportJob")
            .WithSummary("Dispatches the import job for asynchronous background execution.")
            .WithDescription("Enqueues the import job for background processing. Returns 202 Accepted — poll the status endpoint to track progress. The job must be in 'Mapped' status (mappings confirmed). Returns 400 if the job is in an invalid state, or 404 if not found.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{jobId:guid}/dry-run", DryRunAsync)
            .WithName("DryRunImportJob")
            .WithSummary("Executes a dry-run of the import (validates without persisting data).")
            .WithDescription("Runs the full import pipeline (parsing, mapping, validation) without persisting any data. Returns a detailed report with row-level validation results. Use this to preview errors before committing. The job must be in 'Mapped' status.")
            .Produces<ImportReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{jobId:guid}", GetStatusAsync)
            .WithName("GetImportJobStatus")
            .WithSummary("Returns the current status of an import job.")
            .WithDescription("Returns the current state of the import job including status (Created, Previewed, Mapped, Executing, Completed, PartiallyCompleted, Failed, Cancelled), original file name, row counts, and timing information. Returns 404 if not found.")
            .Produces<ImportJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{jobId:guid}", CancelAsync)
            .WithName("CancelImportJob")
            .WithSummary("Cancels an import job that has not yet started execution.")
            .WithDescription("Cancels the import job and deletes the uploaded file from blob storage. Only jobs that have not yet started execution (Executing, Completed, PartiallyCompleted, Failed) can be cancelled. Returns 400 if the job is in a non-cancellable state.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> ExecuteAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        [FromServices] ICommandSender commandSender,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (job.Status != ImportJobStatus.Mapped)
        {
            return TypedResults.Problem(
                detail: $"Cannot execute import job in status '{job.Status}'. Expected: Mapped.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ExecuteImportCommand command = new(job.Id, job.DefinitionName);
        await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted($"/{job.Id}");
    }

    private static async Task<Results<Ok<ImportReportResponse>, ProblemHttpResult>> DryRunAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (job.Status != ImportJobStatus.Mapped)
        {
            return TypedResults.Problem(
                detail: $"Cannot dry-run import job in status '{job.Status}'. Expected: Mapped.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ImportReport report = await orchestrator.DryRunAsync(jobId, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(ImportReportResponse.FromReport(jobId, report));
    }

    private static async Task<Results<Ok<ImportJobResponse>, ProblemHttpResult>> GetStatusAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ImportJobResponse.FromJob(job));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CancelAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportJobWriter jobWriter,
        [FromServices] IDataExchangeFileProvider fileProvider,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            job.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        await fileProvider.DeleteAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
