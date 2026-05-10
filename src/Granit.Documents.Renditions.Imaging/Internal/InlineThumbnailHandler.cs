using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents.Events;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Options;
using Granit.Events;
using Granit.Guids;
using Granit.Imaging;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.Imaging.Internal;

/// <summary>
/// Local event handler that produces a thumbnail synchronously when the freshly
/// uploaded version is an image and the result fits under
/// <see cref="GranitRenditionsOptions.InlineThumbnailMaxBytes"/>. The Ready row is
/// inserted in-band so the F16.4 background handler (which also subscribes to the
/// same event) sees it and short-circuits.
/// </summary>
/// <remarks>
/// Non-image sources and oversize outputs are silently passed through to the
/// background handler — the inline path is an optimisation, never a hard requirement.
/// Any failure during inline generation is logged and swallowed: the background
/// handler will pick the rendition up on its own scheduling.
/// </remarks>
internal sealed partial class InlineThumbnailHandler(
    IImageProcessor processor,
    IBlobStorage blobStorage,
    System.Net.Http.IHttpClientFactory httpClientFactory,
    IRenditionStore renditionStore,
    ITenantQuotaService quotas,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<GranitRenditionsOptions> options,
    ILogger<InlineThumbnailHandler> logger) : ILocalEventHandler<DocumentVersionAddedEvent>
{
    /// <summary>Named HTTP client used to GET the source blob + PUT the thumbnail bytes.</summary>
    public const string HttpClientName = "granit.documents.renditions.imaging.inline";

    /// <inheritdoc />
    public async Task HandleAsync(DocumentVersionAddedEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (!evt.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GranitRenditionsOptions cfg = options.Value;
        ThumbnailDefaults thumb = cfg.Thumbnail;

        try
        {
            byte[] bytes = await GenerateAsync(
                evt.BlobDescriptorId, thumb, cancellationToken).ConfigureAwait(false);

            if (bytes.Length > cfg.InlineThumbnailMaxBytes)
            {
                LogSkippedOversize(logger, evt.VersionId, bytes.Length, cfg.InlineThumbnailMaxBytes);
                return;
            }

            Guid renditionBlobId = await UploadAsync(bytes, thumb.Format, cancellationToken)
                .ConfigureAwait(false);

            DocumentRendition row = DocumentRendition.CreatePending(
                guidGenerator.Create(),
                evt.TenantId,
                evt.DocumentId,
                evt.VersionId,
                RenditionType.Thumbnail,
                thumb.Format,
                clock.Now);
            row.MarkGenerating();
            row.MarkReady(renditionBlobId, bytes.Length, thumb.Width, thumb.Height, clock.Now);
            await renditionStore.AddAsync(row, cancellationToken).ConfigureAwait(false);

            if (evt.TenantId is { } tid)
            {
                await quotas.IncrementRenditionAsync(tid, bytes.Length, cancellationToken)
                    .ConfigureAwait(false);
            }

            LogGenerated(logger, evt.VersionId, bytes.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInlineFailed(logger, evt.VersionId, ex);
        }
    }

    private async Task<byte[]> GenerateAsync(
        Guid sourceBlobId, ThumbnailDefaults thumb, CancellationToken cancellationToken)
    {
        PresignedDownloadUrl url = await blobStorage
            .CreateDownloadUrlAsync(DocumentBlobContainers.Documents, sourceBlobId, options: null, cancellationToken)
            .ConfigureAwait(false);

        System.Net.Http.HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        await using Stream source = await http.GetStreamAsync(url.Url, cancellationToken).ConfigureAwait(false);

        ImageFormat outputFormat = ResolveFormat(thumb.Format);
        await using IImagePipeline pipeline = processor.Load(source);
        ImageResult result = await pipeline
            .StripMetadata()
            .ConvertTo(outputFormat)
            .Resize(thumb.Width, thumb.Height, ResizeMode.Max)
            .Compress(thumb.Quality)
            .ToResultAsync(cancellationToken)
            .ConfigureAwait(false);
        return result.Content.ToArray();
    }

    private async Task<Guid> UploadAsync(
        byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            DocumentRenditionContainers.Renditions,
            new BlobUploadRequest(
                FileName: $"{guidGenerator.Create():N}",
                ContentType: contentType,
                MaxAllowedBytes: bytes.Length),
            cancellationToken).ConfigureAwait(false);

        System.Net.Http.HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        using System.Net.Http.ByteArrayContent content = new(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }
        using System.Net.Http.HttpRequestMessage request = new(
            new System.Net.Http.HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using System.Net.Http.HttpResponseMessage response = await http
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await blobStorage.ConfirmUploadAsync(
            DocumentRenditionContainers.Renditions,
            ticket.BlobId,
            cancellationToken).ConfigureAwait(false);
        return ticket.BlobId;
    }

    private static ImageFormat ResolveFormat(string mime) => mime?.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
        "image/png" => ImageFormat.Png,
        "image/webp" => ImageFormat.WebP,
        "image/avif" => ImageFormat.Avif,
        "image/gif" => ImageFormat.Gif,
        "image/bmp" => ImageFormat.Bmp,
        "image/tiff" => ImageFormat.Tiff,
        _ => throw new NotSupportedException($"Unsupported inline thumbnail format '{mime}'."),
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Inline thumbnail generated for version {VersionId}: {SizeBytes} bytes")]
    private static partial void LogGenerated(ILogger logger, Guid versionId, int sizeBytes);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Inline thumbnail dropped for version {VersionId}: {SizeBytes} bytes exceeds cap {Cap}; deferring to background.")]
    private static partial void LogSkippedOversize(ILogger logger, Guid versionId, int sizeBytes, long cap);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Inline thumbnail generation failed for version {VersionId}; background handler will retry.")]
    private static partial void LogInlineFailed(ILogger logger, Guid versionId, Exception exception);
}
