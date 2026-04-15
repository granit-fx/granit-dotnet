using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Validates an uploaded file against the import definition constraints,
/// stores it via <see cref="IDataExchangeFileProvider"/>, and creates an <see cref="ImportJob"/>.
/// </summary>
internal sealed partial class ImportUploadService(
    IServiceProvider serviceProvider,
    IDataExchangeFileProvider fileProvider,
    IImportJobWriter jobWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<ImportUploadService> logger) : IImportUploadService
{
    /// <inheritdoc/>
    public async Task<ImportUploadResult> UploadAsync(
        string fileName,
        string contentType,
        long fileSize,
        Stream fileStream,
        string definitionName,
        CancellationToken cancellationToken = default)
    {
        IImportDefinitionDescriptor? descriptor = ImportDefinitionResolver.FindByName(serviceProvider, definitionName);
        if (descriptor is null)
        {
            return ImportUploadResult.Failure($"Unknown import definition '{definitionName}'.");
        }

        if (fileSize == 0)
        {
            return ImportUploadResult.Failure("File is empty.");
        }

        if (fileSize > descriptor.MaxFileSizeMb * 1024L * 1024L)
        {
            return ImportUploadResult.Failure($"File exceeds maximum allowed size of {descriptor.MaxFileSizeMb} MB.");
        }

        if (!descriptor.AllowedMimeTypes.Contains(contentType))
        {
            return ImportUploadResult.Failure($"MIME type '{contentType}' is not allowed. Allowed: {string.Join(", ", descriptor.AllowedMimeTypes)}.");
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
        LogImportJobCreated(logger, job.Id, definitionName, safeFileName);

        return ImportUploadResult.Success(job);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Import job {JobId} created for definition '{DefinitionName}' (file: {FileName})")]
    private static partial void LogImportJobCreated(ILogger logger, Guid jobId, string definitionName, string fileName);
}
