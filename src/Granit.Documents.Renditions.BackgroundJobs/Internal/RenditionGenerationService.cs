using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.BackgroundJobs.Policies;
using Granit.Documents.Renditions.Diagnostics;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Exceptions;
using Granit.Documents.Renditions.Options;
using Granit.Documents.Renditions.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.BackgroundJobs.Internal;

/// <summary>
/// Orchestrates the per-rendition generation flow invoked by
/// <see cref="DocumentVersionAddedRenditionsHandler"/>:
/// <list type="number">
///   <item>find or insert a <see cref="RenditionStatus.Pending"/> row;</item>
///   <item>mark it <see cref="RenditionStatus.Generating"/>;</item>
///   <item>fetch the source bytes, run the pipeline, upload the result;</item>
///   <item>mark the row <see cref="RenditionStatus.Ready"/> and bump
///   <see cref="ITenantQuotaService.IncrementRenditionAsync"/>, or mark
///   <see cref="RenditionStatus.Failed"/> on any error.</item>
/// </list>
/// Concurrency across simultaneous events is bounded by
/// <see cref="GranitRenditionsOptions.MaxConcurrentGenerations"/>.
/// </summary>
internal sealed partial class RenditionGenerationService(
    IRenditionStore renditionStore,
    IRenditionPipeline pipeline,
    IRenditionSourceFetcher sourceFetcher,
    IRenditionResultUploader resultUploader,
    ITenantQuotaService quotas,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<GranitRenditionsOptions> options,
    RenditionsMetrics metrics,
    ILogger<RenditionGenerationService> logger) : IRenditionGenerationService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(options.Value.MaxConcurrentGenerations);

    public void Dispose() => _gate.Dispose();

    public async Task GenerateAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        Guid blobDescriptorId,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DocumentRendition row = await GetOrCreateRowAsync(
                documentId, tenantId, versionId, target, cancellationToken).ConfigureAwait(false);

            if (row.Status == RenditionStatus.Ready)
            {
                return;
            }

            string? tenantTag = tenantId?.ToString();
            var sw = Stopwatch.StartNew();
            using Activity? activity = RenditionsActivitySource.Source.StartActivity(
                RenditionsActivitySource.PipelineExecute);
            try
            {
                row.MarkGenerating();
                await renditionStore.UpdateAsync(row, cancellationToken).ConfigureAwait(false);

                await using Stream source = await sourceFetcher
                    .OpenSourceAsync(blobDescriptorId, cancellationToken)
                    .ConfigureAwait(false);

                RenditionResult result = await pipeline
                    .ExecuteAsync(source, sourceContentType, target, cancellationToken)
                    .ConfigureAwait(false);

                Guid blobId = await resultUploader
                    .UploadAsync(result, cancellationToken)
                    .ConfigureAwait(false);

                row.MarkReady(blobId, result.Content.Length, result.Width, result.Height, clock.Now);
                await renditionStore.UpdateAsync(row, cancellationToken).ConfigureAwait(false);

                if (tenantId is { } tid)
                {
                    await quotas.IncrementRenditionAsync(tid, result.Content.Length, cancellationToken)
                        .ConfigureAwait(false);
                }

                sw.Stop();
                metrics.RecordGenerated(tenantTag, sourceContentType, target.TargetContentType, target.Type.ToString());
                metrics.RecordGenerationDuration(sourceContentType, target.TargetContentType, "pipeline", sw.Elapsed);
                LogGenerated(logger, row.Id, target.Type, target.TargetContentType, result.Content.Length);
            }
            catch (RenditionPipelineException ex)
            {
                row.MarkFailed(ex.Message, clock.Now);
                await renditionStore.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
                metrics.RecordFailed(tenantTag, sourceContentType, target.TargetContentType, nameof(RenditionPipelineException));
                LogFailed(logger, row.Id, target.Type, target.TargetContentType, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.MarkFailed(ex.Message, clock.Now);
                await renditionStore.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
                metrics.RecordFailed(tenantTag, sourceContentType, target.TargetContentType, ex.GetType().Name);
                LogFailedUnexpected(logger, row.Id, target.Type, target.TargetContentType, ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DocumentRendition> GetOrCreateRowAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        RenditionTarget target,
        CancellationToken cancellationToken)
    {
        DocumentRendition? existing = await renditionStore
            .FindAsync(versionId, target.Type, target.TargetContentType, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = DocumentRendition.Create(
            guidGenerator.Create(),
            tenantId,
            documentId,
            versionId,
            target.Type,
            target.TargetContentType,
            clock.Now);
        await renditionStore.AddAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rendition generated: id={RenditionId} type={Type} format={Format} bytes={SizeBytes}")]
    private static partial void LogGenerated(ILogger logger, Guid renditionId, RenditionType type, string format, long sizeBytes);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rendition pipeline failed: id={RenditionId} type={Type} format={Format} reason={Reason}")]
    private static partial void LogFailed(ILogger logger, Guid renditionId, RenditionType type, string format, string reason);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Rendition generation failed unexpectedly: id={RenditionId} type={Type} format={Format}")]
    private static partial void LogFailedUnexpected(ILogger logger, Guid renditionId, RenditionType type, string format, Exception exception);
}
