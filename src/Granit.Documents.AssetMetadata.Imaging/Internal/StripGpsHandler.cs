using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Events;
using Granit.Guids;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.AssetMetadata.Imaging.Internal;

/// <summary>
/// Local event handler that synchronously scrubs GPS coordinates (and other
/// GPS sub-IFD tags) from a freshly-uploaded <c>image/*</c> blob before the
/// F17.4 background extractor runs. Re-uploads the sanitised bytes through
/// <see cref="IBlobStorage"/>, swaps the underlying <c>BlobDescriptorId</c>
/// on the <see cref="DocumentVersion"/> via
/// <see cref="IDocumentService.ReplaceVersionBlobAsync"/> (which also rebalances
/// the tenant quota and emits the <c>DocumentBlobScrubbedEvent</c> for audit),
/// and soft-deletes the original blob so the GPS coordinates never reach
/// cold storage.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ordering vs F17.4.</b> The F17.4 Wolverine handler also subscribes to
/// <see cref="DocumentVersionAddedEvent"/> and dispatches asynchronously through
/// the local Wolverine queue. We do <i>not</i> rely on local-first ordering —
/// it isn't guaranteed once Wolverine is wired. Instead, the F17.4
/// <c>AssetMetadataGenerationService</c> re-fetches the version's current
/// <see cref="DocumentVersion.BlobDescriptorId"/> through <see cref="IDocumentService"/>
/// before opening the source stream, so the extractor always reads the
/// post-scrub bytes — regardless of which handler fires first.
/// </para>
/// <para>
/// <b>Opt-out.</b> Honours <see cref="GranitAssetMetadataOptions.StripGpsOnUpload"/>
/// (default <c>true</c>). When <c>false</c>, the handler short-circuits and the
/// original blob is kept verbatim (matching the corresponding short-circuit in
/// <see cref="ImageMetadataExtractor"/>'s projection).
/// </para>
/// <para>
/// <b>Failures are swallowed</b> (logged at <see cref="LogLevel.Warning"/>). The
/// original blob stays in place so the extractor and downstream consumers still
/// see <i>something</i>; the GPS coordinates remain in cold storage in that case
/// — operators get an alert via the log + metrics to investigate.
/// </para>
/// </remarks>
internal sealed partial class StripGpsHandler(
    IBlobStorage blobStorage,
    IDocumentService documentService,
    IHttpClientFactory httpClientFactory,
    IGuidGenerator guidGenerator,
    AssetMetadataMetrics metrics,
    IOptions<GranitAssetMetadataOptions> options,
    ILogger<StripGpsHandler> logger) : ILocalEventHandler<DocumentVersionAddedEvent>
{
    /// <summary>Audit reason emitted on <c>DocumentBlobScrubbedEvent</c>.</summary>
    public const string ScrubReason = "gps-strip";

    /// <summary>Named <see cref="HttpClient"/> used to GET/PUT the original and scrubbed bytes.</summary>
    public const string HttpClientName = "granit.documents.asset-metadata.imaging.gps-scrub";

    /// <inheritdoc />
    public async Task HandleAsync(DocumentVersionAddedEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        GranitAssetMetadataOptions cfg = options.Value;
        if (!cfg.StripGpsOnUpload)
        {
            return;
        }
        if (!evt.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            byte[] original = await DownloadAsync(evt.BlobDescriptorId, cancellationToken)
                .ConfigureAwait(false);

            byte[]? scrubbed = JpegGpsScrubber.TryScrub(original, evt.ContentType);
            if (scrubbed is null || scrubbed.Length == 0)
            {
                // No GPS payload, or the format isn't supported by the scrubber —
                // keep the original blob, no swap needed.
                return;
            }

            Guid newBlobId = await UploadAsync(scrubbed, evt.ContentType, cancellationToken)
                .ConfigureAwait(false);

            DocumentVersion? updated = await documentService
                .ReplaceVersionBlobAsync(
                    evt.VersionId,
                    newBlobId,
                    scrubbed.Length,
                    ScrubReason,
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated is null)
            {
                LogVersionMissing(logger, evt.VersionId);
                // Best-effort: drop the orphan scrubbed blob to avoid leaking quota
                // bytes on the destination side.
                try
                {
                    await blobStorage
                        .DeleteAsync(DocumentBlobContainers.Documents, newBlobId, "f17.9-orphan", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException)
                {
                    LogOrphanCleanupFailed(logger, newBlobId, cleanupEx);
                }
                return;
            }

            metrics.RecordGpsScrubbed(evt.TenantId?.ToString(), evt.ContentType);
            LogScrubbed(logger, evt.VersionId, original.Length, scrubbed.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogScrubFailed(logger, evt.VersionId, ex);
        }
    }

    private async Task<byte[]> DownloadAsync(Guid blobId, CancellationToken cancellationToken)
    {
        PresignedDownloadUrl url = await blobStorage
            .CreateDownloadUrlAsync(DocumentBlobContainers.Documents, blobId, options: null, cancellationToken)
            .ConfigureAwait(false);

        HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        return await http.GetByteArrayAsync(url.Url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> UploadAsync(byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            DocumentBlobContainers.Documents,
            new BlobUploadRequest(
                FileName: $"scrubbed-{guidGenerator.Create():N}",
                ContentType: contentType,
                MaxAllowedBytes: bytes.Length),
            cancellationToken).ConfigureAwait(false);

        HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        using ByteArrayContent content = new(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }
        using HttpRequestMessage request = new(new HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using HttpResponseMessage response = await http
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        BlobConfirmationResult confirmation = await blobStorage
            .ConfirmUploadAsync(DocumentBlobContainers.Documents, ticket.BlobId, cancellationToken)
            .ConfigureAwait(false);
        if (!confirmation.IsValid)
        {
            throw new InvalidOperationException(
                $"Scrubbed blob {ticket.BlobId} did not pass validation: {confirmation.RejectionReason ?? "unknown"}.");
        }
        return ticket.BlobId;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "GPS scrub completed for version {VersionId}: {OriginalSize} -> {ScrubbedSize} bytes.")]
    private static partial void LogScrubbed(ILogger logger, Guid versionId, int originalSize, int scrubbedSize);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GPS scrub: DocumentVersion {VersionId} not found when applying the blob swap — skipping.")]
    private static partial void LogVersionMissing(ILogger logger, Guid versionId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GPS scrub failed for version {VersionId}; original blob kept verbatim. The async extractor will still drop GPS from the typed projection.")]
    private static partial void LogScrubFailed(ILogger logger, Guid versionId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GPS scrub orphan cleanup failed for blob {BlobId} — the orphan-cleanup job will reclaim it.")]
    private static partial void LogOrphanCleanupFailed(ILogger logger, Guid blobId, Exception exception);
}
