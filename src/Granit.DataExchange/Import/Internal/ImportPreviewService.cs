using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Extracts headers, preview rows, and mapping suggestions for an import job,
/// then transitions it to <see cref="ImportJobStatus.Previewed"/>.
/// </summary>
internal sealed class ImportPreviewService(
    IImportJobReader jobReader,
    IImportJobWriter jobWriter,
    IImportFileProvider fileProvider,
    IMappingSuggestionService mappingService,
    IServiceProvider serviceProvider) : IImportPreviewService
{
    /// <inheritdoc/>
    public async Task<ImportPreviewResult?> PreviewAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        ImportJob? job = await jobReader.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        IImportDefinitionDescriptor? descriptor =
            ImportDefinitionResolver.FindByName(serviceProvider, job.DefinitionName);
        if (descriptor is null)
        {
            return null;
        }

        IEnumerable<IFileParser> parsers = serviceProvider.GetServices<IFileParser>();
        IFileParser? parser = parsers.FirstOrDefault(p => p.CanParse(job.MimeType));
        if (parser is null)
        {
            return null;
        }

        FileParsingOptions parsingOptions = new() { MimeType = job.MimeType };

        await using Stream headerStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> headers = await parser.ExtractHeadersAsync(headerStream, parsingOptions, cancellationToken)
            .ConfigureAwait(false);

        await using Stream previewStream = await fileProvider.OpenAsync(job.BlobReference, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string[]> previewRows = await parser.ReadPreviewAsync(previewStream, parsingOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ImportColumnMapping> suggestions =
            await ImportDefinitionResolver.SuggestMappingsAsync(mappingService, descriptor.EntityType, headers, cancellationToken)
                .ConfigureAwait(false);

        IReadOnlyList<ImportFieldMetadata> fieldMetadata = descriptor.GetFieldMetadata();

        job.MarkAsPreviewed();
        await jobWriter.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

        return new ImportPreviewResult(headers, previewRows, suggestions, fieldMetadata);
    }
}
