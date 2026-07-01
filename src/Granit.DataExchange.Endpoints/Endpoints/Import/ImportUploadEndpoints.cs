using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataExchange.Endpoints.Endpoints.Import;

/// <summary>
/// Upload, preview, and mapping confirmation endpoints (Story #496).
/// </summary>
internal static class ImportUploadEndpoints
{
    /// <summary>
    /// Registers POST /, POST /{jobId}/preview, PUT /{jobId}/mappings onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/jobs", UploadAsync)
            .WithName("UploadImportFile")
            .WithSummary("Uploads a file and creates an import job.")
            .WithDescription("Accepts a multipart/form-data upload with the file and a definitionName field. Validates MIME type and file size against the import definition's constraints. Creates an import job in 'Created' status. The next step is to call the preview endpoint to inspect headers and mapping suggestions.")
            .Produces<ImportJobResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .DisableAntiforgery();

        group.MapPost("/{jobId:guid}/preview", PreviewAsync)
            .WithName("PreviewImportJob")
            .WithSummary("Extracts headers, preview rows, and mapping suggestions for an import job.")
            .WithDescription("Parses the uploaded file to extract column headers, a preview of the first rows, available target field metadata, and AI-assisted mapping suggestions. Transitions the job to 'Previewed' status. Returns 404 if the job does not exist.")
            .Produces<ImportPreviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{jobId:guid}/mappings", ConfirmMappingsAsync)
            .WithName("ConfirmImportMappings")
            .WithSummary("Confirms the column-to-property mappings for an import job.")
            .WithDescription("Saves the user-confirmed column-to-property mappings and transitions the job to 'Mapped' status, making it eligible for execution or dry-run. At least one mapping is required. Returns 404 if the job does not exist. Returns 409 if the concurrency stamp does not match the stored value.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Created<ImportJobResponse>, ProblemHttpResult>> UploadAsync(
        IFormFile file,
        [FromForm] string definitionName,
        [FromServices] IImportUploadService uploadService,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        ImportUploadResult result = await uploadService.UploadAsync(
            file.FileName, file.ContentType, file.Length, stream,
            definitionName, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return TypedResults.Problem(
                detail: result.ErrorDetail,
                statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.Created($"/data-exchange/import/{result.Job!.Id}", ImportJobResponse.FromJob(result.Job));
    }

    private static async Task<Results<Ok<ImportPreviewResponse>, ProblemHttpResult>> PreviewAsync(
        Guid jobId,
        [FromServices] IImportPreviewService previewService,
        CancellationToken cancellationToken)
    {
        ImportPreviewResult? preview = await previewService.PreviewAsync(jobId, cancellationToken)
            .ConfigureAwait(false);

        if (preview is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(new ImportPreviewResponse(
            preview.Headers, preview.PreviewRows, preview.Suggestions, preview.FieldMetadata));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmMappingsAsync(
        Guid jobId,
        ConfirmMappingsRequest request,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportJobWriter jobWriter,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (request.Mappings is null || request.Mappings.Count == 0)
        {
            return TypedResults.Problem(
                detail: "At least one column mapping is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        job.ConfirmMappings(request.Mappings);
        await jobWriter.UpdateAsync(job, request.ConcurrencyStamp, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
