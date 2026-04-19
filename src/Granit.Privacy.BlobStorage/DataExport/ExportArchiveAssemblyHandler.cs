using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Options;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.BlobStorage.DataExport;

/// <summary>
/// Terminal step of the personal-data export pipeline: consumes
/// <see cref="ExportCompletedEto"/>, streams every fragment into a ZIP archive,
/// uploads the archive, and marks the tracker as
/// <see cref="ExportRequestState.Completed"/> / <see cref="ExportRequestState.PartiallyCompleted"/> /
/// <see cref="ExportRequestState.SizeLimitExceeded"/>.
/// </summary>
/// <remarks>
/// <para>
/// Streaming-only: fragments and the final ZIP are copied chunk-by-chunk through a temp file —
/// never buffered to <c>byte[]</c>. An export of a few hundred megabytes of auditing data for
/// an active user is plausible, so materialising the archive in memory would OOM the worker.
/// </para>
/// <para>
/// Presigned download URLs for fragments are requested with
/// <see cref="GranitPrivacyOptions.ArchiveAssemblyDownloadUrlExpiryMinutes"/> (default 15 min) —
/// enough to cover the saga timeout plus Wolverine retry headroom.
/// </para>
/// <para>
/// Fragments whose <c>BlobReferenceId</c> carries the
/// <see cref="PrivacyExportContainerNames.EmptyFragmentPrefix"/> sentinel represent providers
/// that had no data for the user. They are recorded in the manifest's <c>EmptyProviders</c>
/// list and never dereferenced — passing the sentinel to <c>CreateDownloadUrlAsync</c> would
/// raise <c>BlobNotFoundException</c>.
/// </para>
/// </remarks>
public sealed partial class ExportArchiveAssemblyHandler(
    IBlobStorage blobStorage,
    IExportRequestTrackerWriter trackerWriter,
    IHttpClientFactory httpClientFactory,
    IOptions<GranitPrivacyOptions> options,
    TimeProvider timeProvider,
    PrivacyMetrics metrics,
    ILogger<ExportArchiveAssemblyHandler> logger)
{
    /// <summary>Named <see cref="HttpClient"/> used for fragment downloads and archive uploads.</summary>
    public const string HttpClientName = "Granit.Privacy.ArchiveAssembly";

    public async Task HandleAsync(ExportCompletedEto @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        GranitPrivacyOptions opts = options.Value;
        long maxBytes = (long)opts.ExportMaxSizeMb * 1024L * 1024L;
        var downloadTtl = TimeSpan.FromMinutes(opts.ArchiveAssemblyDownloadUrlExpiryMinutes);

        string tempPath = Path.Combine(Path.GetTempPath(), $"granit-privacy-export-{@event.RequestId}.zip");
        long startTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = PrivacyActivitySource.Source.StartActivity(
            PrivacyActivitySource.ArchiveAssemble, ActivityKind.Internal);
        activity?.SetTag("privacy.export.request_id", @event.RequestId);
        activity?.SetTag("privacy.export.is_partial", @event.IsPartial);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        List<string> emptyProviders = [];
        List<ExportManifestFragment> manifestFragments = [];

        try
        {
            await using (FileStream tempStream = new(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            await using (CountingStream countingStream = new(tempStream, maxBytes))
            {
                using (ZipArchive zip = new(countingStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (ReceivedFragment fragment in @event.Fragments)
                    {
                        if (fragment.BlobReferenceId.StartsWith(
                            PrivacyExportContainerNames.EmptyFragmentPrefix, StringComparison.Ordinal))
                        {
                            emptyProviders.Add(fragment.ProviderName);
                            continue;
                        }

                        if (!Guid.TryParse(fragment.BlobReferenceId, out Guid blobId))
                        {
                            LogUnexpectedBlobReference(logger, fragment.ProviderName, fragment.BlobReferenceId, @event.RequestId);
                            continue;
                        }

                        string entryName = await ResolveEntryNameAsync(blobId, fragment, cancellationToken).ConfigureAwait(false);
                        await CopyFragmentToZipAsync(
                            zip, entryName, blobId, downloadTtl, httpClient, cancellationToken).ConfigureAwait(false);

                        manifestFragments.Add(new ExportManifestFragment(
                            fragment.ProviderName, entryName, fragment.ContentType, fragment.BlobReferenceId));
                    }

                    await WriteManifestAsync(zip, @event, emptyProviders, manifestFragments, cancellationToken)
                        .ConfigureAwait(false);
                }

                await countingStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                tempStream.Position = 0;
                await UploadArchiveAsync(@event.RequestId, tempStream, httpClient, cancellationToken)
                    .ConfigureAwait(false);
            }

            ExportRequestState finalState = @event.IsPartial
                ? ExportRequestState.PartiallyCompleted
                : ExportRequestState.Completed;

            await trackerWriter.MarkCompletedAsync(
                @event.RequestId,
                finalState,
                PrivacyExportContainerNames.ArchiveBlobReferenceId(@event.RequestId),
                @event.MissingProviders,
                cancellationToken).ConfigureAwait(false);

            TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RecordArchiveAssembled(
                tenantId: null, status: finalState.ToString(), @event.IsPartial, duration, @event.Regulation);
            LogArchiveAssembled(logger, @event.RequestId, @event.Fragments.Count, emptyProviders.Count, (long)duration.TotalMilliseconds);
        }
        catch (PrivacyExportSizeLimitExceededException ex)
        {
            LogSizeLimitExceeded(logger, @event.RequestId, ex.MaxBytes, ex.ObservedBytes);
            await trackerWriter.MarkCompletedAsync(
                @event.RequestId,
                ExportRequestState.SizeLimitExceeded,
                archiveBlobReferenceId: null,
                @event.MissingProviders,
                cancellationToken).ConfigureAwait(false);
            TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RecordArchiveAssembled(
                tenantId: null,
                status: ExportRequestState.SizeLimitExceeded.ToString(),
                @event.IsPartial,
                duration,
                @event.Regulation);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private async Task<string> ResolveEntryNameAsync(
        Guid blobId, ReceivedFragment fragment, CancellationToken cancellationToken)
    {
        BlobDescriptor? descriptor = await blobStorage.GetDescriptorAsync(
            PrivacyExportContainerNames.FragmentContainer, blobId, cancellationToken).ConfigureAwait(false);
        string? original = descriptor?.OriginalFileName;
        if (!string.IsNullOrWhiteSpace(original))
        {
            return original;
        }

        return $"{fragment.ProviderName}{ExtensionFor(fragment.ContentType)}";
    }

    private async Task CopyFragmentToZipAsync(
        ZipArchive zip,
        string entryName,
        Guid blobId,
        TimeSpan downloadTtl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        PresignedDownloadUrl downloadUrl = await blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobId,
            new DownloadUrlOptions(Expiry: downloadTtl),
            cancellationToken).ConfigureAwait(false);

        using HttpResponseMessage response = await httpClient
            .GetAsync(downloadUrl.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await contentStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteManifestAsync(
        ZipArchive zip,
        ExportCompletedEto @event,
        IReadOnlyList<string> emptyProviders,
        IReadOnlyList<ExportManifestFragment> fragments,
        CancellationToken cancellationToken)
    {
        ExportManifest manifest = new(
            @event.RequestId,
            @event.UserId,
            @event.Regulation,
            @event.RequestedAt,
            timeProvider.GetUtcNow(),
            @event.IsPartial,
            @event.MissingProviders,
            emptyProviders,
            fragments);

        ZipArchiveEntry entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, manifest, ManifestJsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UploadArchiveAsync(
        Guid requestId, FileStream archiveStream, HttpClient httpClient, CancellationToken cancellationToken)
    {
        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            new BlobUploadRequest(
                FileName: $"personal-data-export-{requestId}.zip",
                ContentType: "application/zip",
                MaxAllowedBytes: archiveStream.Length),
            cancellationToken).ConfigureAwait(false);

        using StreamContent content = new(archiveStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Headers.ContentLength = archiveStream.Length;
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }

        using HttpRequestMessage request = new(new HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await blobStorage.ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer, ticket.BlobId, cancellationToken)
            .ConfigureAwait(false);
    }

    private void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException ex)
        {
            LogTempFileCleanupFailed(logger, tempPath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogTempFileCleanupFailed(logger, tempPath, ex.Message);
        }
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/json" => ".json",
        "application/xml" or "text/xml" => ".xml",
        "text/csv" => ".csv",
        "text/plain" => ".txt",
        "application/pdf" => ".pdf",
        _ => ".bin",
    };

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export {RequestId}: archive assembled with {FragmentCount} fragment(s), {EmptyCount} empty, in {ElapsedMs} ms")]
    private static partial void LogArchiveAssembled(ILogger logger, Guid requestId, int fragmentCount, int emptyCount, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Privacy export {RequestId}: provider {Provider} returned unparseable BlobReferenceId '{BlobReferenceId}' — skipping")]
    private static partial void LogUnexpectedBlobReference(ILogger logger, string provider, string blobReferenceId, Guid requestId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Privacy export {RequestId}: archive exceeded size limit of {MaxBytes} bytes (observed {ObservedBytes}); marking SizeLimitExceeded")]
    private static partial void LogSizeLimitExceeded(ILogger logger, Guid requestId, long maxBytes, long observedBytes);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Privacy export: failed to delete temp archive {TempPath}: {Reason}")]
    private static partial void LogTempFileCleanupFailed(ILogger logger, string tempPath, string reason);
}
