using System.Text.Json;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Endpoints.Endpoints.Import;

/// <summary>
/// Report and correction file endpoints (Story #498).
/// </summary>
internal static class ImportReportEndpoints
{
    /// <summary>
    /// Registers GET /{jobId}/report, GET /{jobId}/correction-file onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{jobId:guid}/report", GetReportAsync)
            .WithName("GetImportReport")
            .WithSummary("Returns the import execution report for a completed job.")
            .WithDescription("Returns the detailed execution report including total rows processed, success/failure counts, and per-row error details. Available after execution or dry-run completes. Returns 404 if the job does not exist or no report has been generated yet.")
            .Produces<ImportReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{jobId:guid}/correction-file", GetCorrectionFileAsync)
            .WithName("GetImportCorrectionFile")
            .WithSummary("Downloads a correction file containing only the failed rows with error annotations.")
            .WithDescription("Generates and streams a file containing only the rows that failed validation or import, annotated with error messages. The file format matches the original upload. Users can fix the errors and re-upload. Returns 204 if there are no failed rows, or 404 if the job or report does not exist.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<ImportReportResponse>, ProblemHttpResult>> GetReportAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || string.IsNullOrEmpty(job.ReportJson))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        ImportReport? report = JsonSerializer.Deserialize<ImportReport>(job.ReportJson);
        if (report is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ImportReportResponse.FromReport(jobId, report));
    }

    private static async Task<Results<FileStreamHttpResult, NoContent, ProblemHttpResult>> GetCorrectionFileAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportFileProvider fileProvider,
        [FromServices] IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || string.IsNullOrEmpty(job.ReportJson))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        ImportReport? report = JsonSerializer.Deserialize<ImportReport>(job.ReportJson);
        if (report is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (report.RowErrors.Count == 0)
        {
            return TypedResults.NoContent();
        }

        ICorrectionFileGenerator? generator =
            serviceProvider.GetService<ICorrectionFileGenerator>();
        if (generator is null)
        {
            return TypedResults.NoContent();
        }

        await using Stream originalStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
        FileParsingOptions parsingOptions = new() { MimeType = job.MimeType };

        Stream correctionStream = await generator.GenerateAsync(
            originalStream, job.MimeType, report, parsingOptions, cancellationToken).ConfigureAwait(false);

        string correctionFileName = $"corrections_{Path.GetFileName(job.OriginalFileName)}";
        return TypedResults.File(correctionStream, job.MimeType, correctionFileName);
    }
}
