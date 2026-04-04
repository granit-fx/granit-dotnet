using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.DataExchange.Endpoints.Internal.Import;

/// <summary>
/// Orchestrates import file upload: definition resolution, file validation, blob storage,
/// and import job creation.
/// </summary>
internal sealed partial class ImportUploadOrchestrator(
    IServiceProvider serviceProvider,
    IImportFileProvider fileProvider,
    IImportJobWriter jobWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<ImportUploadOrchestrator> logger)
{
    /// <summary>
    /// Result of a successful upload operation.
    /// </summary>
    internal sealed record UploadResult(ImportJob Job);

    /// <summary>
    /// Result of a failed upload operation.
    /// </summary>
    internal sealed record UploadError(string Detail);

    /// <summary>
    /// Validates the file against the import definition constraints, stores it, and creates an import job.
    /// </summary>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME type of the file.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="fileStream">The file content stream.</param>
    /// <param name="definitionName">Name of the import definition to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Either a successful <see cref="UploadResult"/> or an <see cref="UploadError"/>.</returns>
    internal async Task<(UploadResult? Result, UploadError? Error)> UploadAsync(
        string fileName,
        string contentType,
        long fileSize,
        Stream fileStream,
        string definitionName,
        CancellationToken cancellationToken)
    {
        IImportDefinitionDescriptor? descriptor =
            ImportDefinitionResolver.FindByName(serviceProvider, definitionName);
        if (descriptor is null)
        {
            return (null, new UploadError($"Unknown import definition '{definitionName}'."));
        }

        if (fileSize == 0)
        {
            return (null, new UploadError("File is empty."));
        }

        if (fileSize > descriptor.MaxFileSizeMb * 1024L * 1024L)
        {
            return (null, new UploadError($"File exceeds maximum allowed size of {descriptor.MaxFileSizeMb} MB."));
        }

        if (!descriptor.AllowedMimeTypes.Contains(contentType))
        {
            return (null, new UploadError($"MIME type '{contentType}' is not allowed. Allowed: {string.Join(", ", descriptor.AllowedMimeTypes)}."));
        }

        string safeFileName = Path.GetFileName(fileName);
        string blobReference = await fileProvider.SaveAsync(safeFileName, fileStream, cancellationToken)
            .ConfigureAwait(false);

        var job = ImportJob.Create(
            guidGenerator.Create(),
            descriptor.Name,
            descriptor.EntityType.Name,
            safeFileName,
            contentType,
            fileSize,
            blobReference);
        job.CreatedAt = clock.Now;

        await jobWriter.CreateAsync(job, cancellationToken).ConfigureAwait(false);
        Log.ImportJobCreated(logger, job.Id, definitionName, safeFileName);

        return (new UploadResult(job), null);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Import job {JobId} created for definition '{DefinitionName}' (file: {FileName})")]
        public static partial void ImportJobCreated(ILogger logger, Guid jobId, string definitionName, string fileName);
    }
}
