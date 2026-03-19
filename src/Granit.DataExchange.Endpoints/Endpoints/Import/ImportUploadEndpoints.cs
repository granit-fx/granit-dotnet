using System.Text.Json;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Internal.Export;
using Granit.DataExchange.Endpoints.Internal.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        group.MapPost("/", UploadAsync)
            .WithName("UploadImportFile")
            .WithSummary("Uploads a file and creates an import job.")
            .WithDescription("Accepts a multipart/form-data upload with the file and a definitionName field. Validates MIME type and file size against the import definition's constraints. Creates an import job in 'Created' status. The next step is to call the preview endpoint to inspect headers and mapping suggestions.")
            .DisableAntiforgery();

        group.MapPost("/{jobId:guid}/preview", PreviewAsync)
            .WithName("PreviewImportJob")
            .WithSummary("Extracts headers, preview rows, and mapping suggestions for an import job.")
            .WithDescription("Parses the uploaded file to extract column headers, a preview of the first rows, available target field metadata, and AI-assisted mapping suggestions. Transitions the job to 'Previewed' status. Returns 404 if the job does not exist.");

        group.MapPut("/{jobId:guid}/mappings", ConfirmMappingsAsync)
            .WithName("ConfirmImportMappings")
            .WithSummary("Confirms the column-to-property mappings for an import job.")
            .WithDescription("Saves the user-confirmed column-to-property mappings and transitions the job to 'Mapped' status, making it eligible for execution or dry-run. At least one mapping is required. Returns 404 if the job does not exist.");

        return group;
    }

    private static async Task<Results<Created<ImportJobResponse>, ProblemHttpResult>> UploadAsync(
        IFormFile file,
        [FromForm] string definitionName,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IImportFileProvider fileProvider,
        [FromServices] IImportJobWriter jobWriter,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        IImportDefinitionDescriptor? descriptor =
            ImportDefinitionResolver.FindByName(serviceProvider, definitionName);
        if (descriptor is null)
        {
            return TypedResults.Problem(
                detail: $"Unknown import definition '{definitionName}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length == 0)
        {
            return TypedResults.Problem(
                detail: "File is empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > descriptor.MaxFileSizeMb * 1024L * 1024L)
        {
            return TypedResults.Problem(
                detail: $"File exceeds maximum allowed size of {descriptor.MaxFileSizeMb} MB.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!descriptor.AllowedMimeTypes.Contains(file.ContentType))
        {
            return TypedResults.Problem(
                detail: $"MIME type '{file.ContentType}' is not allowed. Allowed: {string.Join(", ", descriptor.AllowedMimeTypes)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using Stream stream = file.OpenReadStream();
        string blobReference = await fileProvider.SaveAsync(file.FileName, stream, cancellationToken).ConfigureAwait(false);

        var job = ImportJob.Create(
            guidGenerator.Create(),
            descriptor.Name,
            descriptor.EntityType.Name,
            file.FileName,
            file.ContentType,
            file.Length,
            blobReference);
        job.CreatedAt = clock.Now;

        await jobWriter.CreateAsync(job, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/{job.Id}", ImportJobResponse.FromJob(job));
    }

    private static async Task<Results<Ok<ImportPreviewResponse>, NotFound>> PreviewAsync(
        Guid jobId,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportJobWriter jobWriter,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IImportFileProvider fileProvider,
        [FromServices] IMappingSuggestionService mappingService,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        IImportDefinitionDescriptor? descriptor =
            ImportDefinitionResolver.FindByName(serviceProvider, job.DefinitionName);
        if (descriptor is null)
        {
            return TypedResults.NotFound();
        }

        IEnumerable<IFileParser> parsers = serviceProvider.GetServices<IFileParser>();
        IFileParser? parser = parsers.FirstOrDefault(p => p.CanParse(job.MimeType));
        if (parser is null)
        {
            return TypedResults.NotFound();
        }

        FileParsingOptions parsingOptions = new() { MimeType = job.MimeType };

        await using Stream headerStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> headers = await parser.ExtractHeadersAsync(headerStream, parsingOptions, cancellationToken).ConfigureAwait(false);

        await using Stream previewStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string[]> previewRows = await parser.ReadPreviewAsync(previewStream, parsingOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ImportColumnMapping> suggestions =
            await ImportDefinitionResolver.SuggestMappingsAsync(mappingService, descriptor.EntityType, headers, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ImportFieldMetadata> fieldMetadata = descriptor.GetFieldMetadata();

        job.MarkAsPreviewed();
        await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new ImportPreviewResponse(headers, previewRows, suggestions, fieldMetadata));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ConfirmMappingsAsync(
        Guid jobId,
        ConfirmMappingsRequest request,
        [FromServices] IImportJobReader jobReader,
        [FromServices] IImportJobWriter jobWriter,
        CancellationToken cancellationToken)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Mappings is null || request.Mappings.Count == 0)
        {
            return TypedResults.Problem(
                detail: "At least one column mapping is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        job.ConfirmMappings(JsonSerializer.Serialize(request.Mappings));
        await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
