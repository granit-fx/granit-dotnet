using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Domain.ValueObjects;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.BlobStorage.DataExport;

/// <summary>
/// Sharded ZIP assembly service. Consumes the saga's terminal
/// <see cref="ExportCompletedEto"/> via a background-job handler, verifies every
/// fragment's HMAC capability before reading bytes (closes VULN-001 / VULN-102),
/// streams the content into one or more size-capped ZIP64 shards using
/// <see cref="ShardingArchiveWriter"/>, writes a simple manifest sidecar, and
/// records the manifest blob reference on the tracker as the request's archive
/// pointer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single-transit reads.</b> Fragments still flow through a presigned download
/// URL because the existing fragment uploader writes via the public
/// <see cref="IBlobStorage"/> contract. Provider-aware native reads land in
/// P6.3d / P6.4 once <see cref="IBlobStoreProvider.OpenReadAsync"/> grows a
/// public adapter on <see cref="IBlobStorage"/>.
/// </para>
/// <para>
/// <b>Checkpointing.</b> Resumable state is recorded on entry (set to "fresh
/// run" / 0) and cleared on completion. Mid-flight per-shard checkpoints (true
/// crash-resume) land in a follow-up sub-PR — the interface and store are in
/// place so the upgrade is a localised change inside this service.
/// </para>
/// <para>
/// <b>HMAC verification.</b> Each fragment is verified via
/// <see cref="IExportHmacSigner.Verify"/> with parameters reconstructed from the
/// <see cref="ReceivedFragment"/> values. Empty-sentinel fragments
/// (<see cref="PrivacyFragmentUploader.EmptyFragmentKind"/>) carry no HMAC and
/// are recorded in the manifest's <c>emptyProviders</c> list without a blob
/// read.
/// </para>
/// </remarks>
internal sealed partial class PrivacyExportAssemblyService(
    IBlobStorage blobStorage,
    IBlobStoreProvider blobStoreProvider,
    IExportHmacSigner hmacSigner,
    IExportAssemblyCheckpointStore checkpointStore,
    IExportRequestTrackerWriter trackerWriter,
    IHttpClientFactory httpClientFactory,
    IOptions<GranitPrivacyOptions> options,
    TimeProvider timeProvider,
    PrivacyMetrics metrics,
    ILogger<PrivacyExportAssemblyService> logger) : IPrivacyExportAssemblyService
{
    /// <summary>Named <see cref="HttpClient"/> used for fragment downloads.</summary>
    public const string HttpClientName = "Granit.Privacy.ArchiveAssembly";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task AssembleAsync(ExportCompletedEto completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);

        GranitPrivacyOptions opts = options.Value;
        var downloadTtl = TimeSpan.FromMinutes(opts.ArchiveAssemblyDownloadUrlExpiryMinutes);
        long shardMaxBytes = (long)opts.ExportShardMaxSizeMb * 1024L * 1024L;

        long startTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = PrivacyActivitySource.Source.StartActivity(
            PrivacyActivitySource.ArchiveAssemble, ActivityKind.Internal);
        activity?.SetTag("privacy.export.request_id", completion.RequestId);
        activity?.SetTag("privacy.export.is_partial", completion.IsPartial);

        // Mark the checkpoint slot — a future sub-PR will write per-shard progress here.
        await checkpointStore.SetAsync(
            completion.RequestId,
            completion.TenantId,
            new ExportAssemblyCheckpoint(
                LastCompletedShardIndex: -1,
                NextFragmentIndex: 0,
                CompletedShardObjectKeys: []),
            cancellationToken).ConfigureAwait(false);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        List<string> emptyProviders = [];
        List<ExportManifestFragment> manifestFragments = [];

        string objectKeyPrefix = BuildObjectKeyPrefix(completion.RequestId);
        await using ShardingArchiveWriter writer = new(
            blobStoreProvider, PrivacyExportContainerBucket(opts), objectKeyPrefix, shardMaxBytes);

        DateTimeOffset hmacExpiryWindow = timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(opts.ExportTimeoutMinutes * 4);

        try
        {
            foreach (ReceivedFragment fragment in completion.Fragments)
            {
                if (string.Equals(fragment.FragmentKind, PrivacyFragmentUploader.EmptyFragmentKind, StringComparison.Ordinal))
                {
                    emptyProviders.Add(fragment.ProviderName);
                    continue;
                }

                if (!Guid.TryParse(fragment.BlobReferenceId.Value, out Guid blobId))
                {
                    LogUnexpectedBlobReference(logger, fragment.ProviderName, fragment.BlobReferenceId.Value, completion.RequestId);
                    continue;
                }

                if (!VerifyFragmentTag(completion, fragment, blobId, hmacExpiryWindow))
                {
                    LogIntegrityTagRejected(logger, fragment.ProviderName, completion.RequestId);
                    throw new InvalidOperationException(
                        $"HMAC integrity tag verification failed for provider '{fragment.ProviderName}' on request {completion.RequestId}.");
                }

                string entryName = await ResolveEntryNameAsync(fragment, blobId, cancellationToken).ConfigureAwait(false);
                await CopyFragmentToWriterAsync(writer, entryName, fragment, blobId, downloadTtl, httpClient, cancellationToken)
                    .ConfigureAwait(false);

                manifestFragments.Add(new ExportManifestFragment(
                    fragment.ProviderName, entryName, fragment.ContentType, fragment.BlobReferenceId));
                // ExportManifestFragment.FileName carries the resolved entry name.
            }

            IReadOnlyList<ShardManifest> shards = await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);

            BlobReference manifestBlobReference = await UploadManifestAsync(
                completion, emptyProviders, manifestFragments, shards, httpClient, cancellationToken)
                .ConfigureAwait(false);

            ExportRequestState finalState = completion.IsPartial
                ? ExportRequestState.PartiallyCompleted
                : ExportRequestState.Completed;

            await trackerWriter.MarkCompletedAsync(
                completion.RequestId,
                finalState,
                manifestBlobReference,
                completion.MissingProviders,
                cancellationToken).ConfigureAwait(false);

            await checkpointStore.ClearAsync(completion.RequestId, completion.TenantId, cancellationToken).ConfigureAwait(false);

            TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RecordArchiveAssembled(
                tenantId: completion.TenantId, status: finalState.ToString(), completion.IsPartial, duration, completion.Regulation);
            LogArchiveAssembled(logger, completion.RequestId, shards.Count, completion.Fragments.Count, emptyProviders.Count, (long)duration.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
            LogAssemblyFailed(logger, completion.RequestId, ex.GetType().Name);
            metrics.RecordArchiveAssembled(
                tenantId: completion.TenantId,
                status: "failed",
                completion.IsPartial,
                duration,
                completion.Regulation);
            throw;
        }
    }

    private async Task<string> ResolveEntryNameAsync(ReceivedFragment fragment, Guid blobId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(fragment.EntryPath))
        {
            return fragment.EntryPath;
        }

        BlobDescriptor? descriptor = await blobStorage.GetDescriptorAsync(
            fragment.SourceContainer, blobId, cancellationToken).ConfigureAwait(false);
        string? original = descriptor?.OriginalFileName;
        if (!string.IsNullOrWhiteSpace(original))
        {
            return original;
        }

        return $"{fragment.ProviderName}{ExtensionFor(fragment.ContentType)}";
    }

    private async Task CopyFragmentToWriterAsync(
        ShardingArchiveWriter writer,
        string entryName,
        ReceivedFragment fragment,
        Guid blobId,
        TimeSpan downloadTtl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        PresignedDownloadUrl downloadUrl = await blobStorage.CreateDownloadUrlAsync(
            fragment.SourceContainer,
            blobId,
            new DownloadUrlOptions(Expiry: downloadTtl),
            cancellationToken).ConfigureAwait(false);

        using HttpResponseMessage response = await httpClient
            .GetAsync(downloadUrl.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await writer.AppendAsync(entryName, fragment.ContentType, contentStream, cancellationToken)
            .ConfigureAwait(false);
    }

    private bool VerifyFragmentTag(
        ExportCompletedEto completion,
        ReceivedFragment fragment,
        Guid blobId,
        DateTimeOffset expiry)
    {
        if (string.IsNullOrEmpty(fragment.IntegrityTag))
        {
            return false;
        }

        return hmacSigner.Verify(
            new ExportHmacParameters(
                RequestId: completion.RequestId,
                SubjectUserId: completion.UserId,
                ProviderName: fragment.ProviderName,
                FragmentKind: fragment.FragmentKind,
                SourceContainer: fragment.SourceContainer,
                SourceBlobId: blobId,
                EntryPath: fragment.EntryPath ?? string.Empty,
                ExpiresAt: expiry),
            fragment.IntegrityTag);
    }

    private async Task<BlobReference> UploadManifestAsync(
        ExportCompletedEto completion,
        IReadOnlyList<string> emptyProviders,
        IReadOnlyList<ExportManifestFragment> manifestFragments,
        IReadOnlyList<ShardManifest> shards,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            schemaVersion = 1,
            requestId = completion.RequestId,
            userId = completion.UserId,
            regulation = completion.Regulation,
            requestedAt = completion.RequestedAt,
            completedAt = timeProvider.GetUtcNow(),
            isPartial = completion.IsPartial,
            missingProviders = completion.MissingProviders,
            emptyProviders,
            shards = shards.Select(s => new
            {
                index = s.Index,
                objectKey = s.ObjectKey,
                compressedSizeBytes = s.CompressedSizeBytes,
            }).ToArray(),
            fragments = manifestFragments,
        };

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonOptions);

        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            new BlobUploadRequest(
                FileName: $"personal-data-export-{completion.RequestId}-manifest.json",
                ContentType: "application/json",
                MaxAllowedBytes: payload.Length),
            cancellationToken).ConfigureAwait(false);

        using ByteArrayContent content = new(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }

        using HttpRequestMessage request = new(new HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await blobStorage.ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer, ticket.BlobId, cancellationToken).ConfigureAwait(false);

        return BlobReference.Create(ticket.BlobId.ToString());
    }

    private static string BuildObjectKeyPrefix(Guid requestId) =>
        $"personal-data-export/{requestId}";

    // The shard writer talks to the low-level IBlobStoreProvider, which routes by
    // bucket name (not container). The privacy export uses a single bucket name —
    // re-use the fragment-container constant so providers that wire one bucket per
    // container resolve consistently.
    private static string PrivacyExportContainerBucket(GranitPrivacyOptions _) =>
        PrivacyExportContainerNames.FragmentContainer;

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/json" => ".json",
        "application/xml" or "text/xml" => ".xml",
        "text/csv" => ".csv",
        "text/plain" => ".txt",
        "application/pdf" => ".pdf",
        _ => ".bin",
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export {RequestId}: assembled into {ShardCount} shard(s) ({FragmentCount} fragment(s), {EmptyCount} empty) in {ElapsedMs} ms")]
    private static partial void LogArchiveAssembled(ILogger logger, Guid requestId, int shardCount, int fragmentCount, int emptyCount, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Privacy export {RequestId}: provider {Provider} returned unparseable BlobReferenceId '{BlobReferenceId}' — skipping")]
    private static partial void LogUnexpectedBlobReference(ILogger logger, string provider, string blobReferenceId, Guid requestId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Privacy export {RequestId}: HMAC integrity tag rejected for provider {Provider} — refusing to read source blob")]
    private static partial void LogIntegrityTagRejected(ILogger logger, string provider, Guid requestId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Privacy export {RequestId}: assembly failed ({ExceptionType}) — checkpoint preserved for retry")]
    private static partial void LogAssemblyFailed(ILogger logger, Guid requestId, string exceptionType);
}

