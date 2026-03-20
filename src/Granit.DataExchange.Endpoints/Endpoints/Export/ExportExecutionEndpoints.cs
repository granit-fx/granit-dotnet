using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Internal.Export;
using Granit.DataExchange.Endpoints.Internal.Import;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataExchange.Endpoints.Endpoints.Export;

/// <summary>
/// Export job creation, status polling, and file download endpoints.
/// </summary>
internal static class ExportExecutionEndpoints
{
    /// <summary>
    /// Registers POST /jobs, GET /jobs/{jobId}, GET /jobs/{jobId}/download onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapExportExecutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/jobs", CreateExportJobAsync)
            .WithName("CreateExportJob")
            .WithSummary("Creates and dispatches an export job (sync or background).")
            .WithDescription("Creates an export job for the given definition, format, and field selection. Small datasets may complete synchronously; larger ones are dispatched for background processing. Poll the status endpoint to track progress. Returns 400 if the definition name or format is invalid.")
            .Produces<ExportJobResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/jobs/{jobId:guid}", GetJobStatusAsync)
            .WithName("GetExportJobStatus")
            .WithSummary("Returns the current status of an export job.")
            .WithDescription("Returns the current status of the export job (Created, Processing, Completed, Failed). Once the status is Completed, the download endpoint becomes available. Returns 404 if the job ID is not found.")
            .Produces<ExportJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/jobs/{jobId:guid}/download", DownloadAsync)
            .WithName("DownloadExportFile")
            .WithSummary("Downloads the generated export file for a completed job.")
            .WithDescription("Streams the generated export file (xlsx, csv, etc.) as a binary download. The Content-Type and Content-Disposition headers are set according to the export format. Returns 404 if the job does not exist, or 400 if the job has not completed yet.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<Results<Created<ExportJobResponse>, ProblemHttpResult>> CreateExportJobAsync(
        CreateExportJobRequest request,
        [FromServices] IExportOrchestrator orchestrator,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        IExportDefinitionDescriptor? descriptor =
            ExportDefinitionResolver.FindByName(serviceProvider, request.DefinitionName);
        if (descriptor is null)
        {
            return TypedResults.Problem(
                detail: $"Unknown export definition '{request.DefinitionName}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!descriptor.SupportedFormats.Contains(request.Format, StringComparer.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: $"Format '{request.Format}' is not supported. Allowed: {string.Join(", ", descriptor.SupportedFormats)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ExportRequest exportRequest = new(
            request.DefinitionName,
            request.Format,
            request.SelectedFields,
            request.IncludeIdForImport,
            request.Sort,
            request.Filter,
            request.Presets,
            request.Search);

        ExportJobResult result = await orchestrator.ExportAsync(exportRequest, cancellationToken).ConfigureAwait(false);

        ExportJob? job = await orchestrator.GetJobAsync(result.JobId, cancellationToken).ConfigureAwait(false);
        ExportJobResponse response = job is not null
            ? ExportJobResponse.FromJob(job)
            : new ExportJobResponse(result.JobId, request.DefinitionName, request.Format,
                result.Status, null, null, null, clock.Now, null);

        return TypedResults.Created($"/jobs/{result.JobId}", response);
    }

    private static async Task<Results<Ok<ExportJobResponse>, NotFound>> GetJobStatusAsync(
        Guid jobId,
        [FromServices] IExportOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        ExportJob? job = await orchestrator.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ExportJobResponse.FromJob(job));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> DownloadAsync(
        Guid jobId,
        [FromServices] IExportOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        ExportJob? job = await orchestrator.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (job.Status != ExportJobStatus.Completed)
        {
            return TypedResults.Problem(
                detail: $"Export job is not completed. Current status: '{job.Status}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ExportDownload? download = await orchestrator.GetDownloadAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (download is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(download.Content, download.MimeType, download.FileName);
    }
}
