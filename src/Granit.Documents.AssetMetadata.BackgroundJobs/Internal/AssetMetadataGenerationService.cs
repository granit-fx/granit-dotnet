using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Exceptions;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.AssetMetadata.Pipeline;
using Granit.Documents.Domain;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.AssetMetadata.BackgroundJobs.Internal;

/// <summary>
/// Orchestrates the per-version extraction flow invoked by the F17.4 handler:
/// <list type="number">
///   <item>find or insert a <see cref="AssetMetadataStatus.Pending"/> row;</item>
///   <item>skip when the row is already <see cref="AssetMetadataStatus.Ready"/>;</item>
///   <item>mark it <see cref="AssetMetadataStatus.Extracting"/>;</item>
///   <item>fetch the source bytes, run every extractor, merge results;</item>
///   <item>mark the row <see cref="AssetMetadataStatus.Ready"/> — or
///   <see cref="AssetMetadataStatus.Failed"/> on pipeline failure.</item>
/// </list>
/// Concurrency across simultaneous events is bounded by
/// <see cref="GranitAssetMetadataOptions.MaxConcurrentExtractions"/>.
/// </summary>
internal sealed partial class AssetMetadataGenerationService(
    IAssetMetadataStore store,
    IAssetMetadataPipeline pipeline,
    IAssetMetadataSourceFetcher sourceFetcher,
    IDocumentService documentService,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<GranitAssetMetadataOptions> options,
    ILogger<AssetMetadataGenerationService> logger) : IAssetMetadataGenerationService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(options.Value.MaxConcurrentExtractions);

    public void Dispose() => _gate.Dispose();

    public async Task ExtractAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        Guid blobDescriptorId,
        string sourceContentType,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DocumentAssetMetadata row = await GetOrCreateRowAsync(
                documentId, tenantId, versionId, sourceContentType, cancellationToken).ConfigureAwait(false);

            if (row.Status == AssetMetadataStatus.Ready)
            {
                return;
            }

            try
            {
                row.MarkExtracting();
                await store.UpdateAsync(row, cancellationToken).ConfigureAwait(false);

                // F17.9 — the StripGpsHandler runs on the same DocumentVersionAddedEvent
                // and may have swapped the version's BlobDescriptorId for a scrubbed one
                // between event publication and this handler being dispatched (local vs.
                // Wolverine queue ordering is not guaranteed). Re-read the current
                // BlobDescriptorId from the DocumentVersion so the extractor always sees
                // the post-scrub bytes. Falls back to the event's snapshot if the lookup
                // returns null (version deleted under us).
                Guid effectiveBlobId = blobDescriptorId;
                DocumentVersion? current = await documentService
                    .GetVersionByIdAsync(versionId, cancellationToken)
                    .ConfigureAwait(false);
                if (current is not null && current.BlobDescriptorId != Guid.Empty)
                {
                    effectiveBlobId = current.BlobDescriptorId;
                }

                await using Stream source = await sourceFetcher
                    .OpenSourceAsync(effectiveBlobId, cancellationToken)
                    .ConfigureAwait(false);

                IReadOnlyList<AssetMetadataResult> results = await pipeline
                    .ExtractAsync(source, sourceContentType, cancellationToken)
                    .ConfigureAwait(false);

                foreach (AssetMetadataResult result in results)
                {
                    row.ApplyExtraction(result);
                }

                row.MarkReady(clock.Now);
                await store.UpdateAsync(row, cancellationToken).ConfigureAwait(false);

                LogExtracted(logger, row.Id, sourceContentType, row.ExtractorCount);
            }
            catch (AssetMetadataExtractionException ex)
            {
                row.MarkFailed(ex.Message, clock.Now);
                await store.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
                LogFailed(logger, row.Id, sourceContentType, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.MarkFailed(ex.Message, clock.Now);
                await store.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
                LogFailedUnexpected(logger, row.Id, sourceContentType, ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DocumentAssetMetadata> GetOrCreateRowAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        string sourceContentType,
        CancellationToken cancellationToken)
    {
        DocumentAssetMetadata? existing = await store
            .GetByVersionAsync(versionId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = DocumentAssetMetadata.Create(
            guidGenerator.Create(),
            tenantId,
            documentId,
            versionId,
            sourceContentType,
            clock.Now);
        await store.AddAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Asset metadata extracted: id={MetadataId} source={SourceContentType} extractors={ExtractorCount}")]
    private static partial void LogExtracted(ILogger logger, Guid metadataId, string sourceContentType, int extractorCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Asset metadata pipeline failed: id={MetadataId} source={SourceContentType} reason={Reason}")]
    private static partial void LogFailed(ILogger logger, Guid metadataId, string sourceContentType, string reason);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Asset metadata extraction failed unexpectedly: id={MetadataId} source={SourceContentType}")]
    private static partial void LogFailedUnexpected(ILogger logger, Guid metadataId, string sourceContentType, Exception exception);
}
