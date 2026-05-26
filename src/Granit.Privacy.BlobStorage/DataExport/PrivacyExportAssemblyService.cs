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
using Granit.Privacy.DataExport.Exceptions;
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
    IExportContentSigner contentSigner,
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

        // Resume from a prior crashed run, or start fresh. The checkpoint granularity
        // is the closed shard — a half-written ZIP or an open multipart upload is not
        // recoverable, so resume skips fragments already absorbed into a fully-committed
        // shard and reopens the writer at the next shard index.
        ExportAssemblyCheckpoint? resumeFrom = await checkpointStore
            .GetAsync(completion.RequestId, completion.TenantId, cancellationToken)
            .ConfigureAwait(false);

        int startFragmentIndex = resumeFrom?.NextFragmentIndex ?? 0;
        int startShardIndex = (resumeFrom?.LastCompletedShardIndex ?? -1) + 1;
        IReadOnlyList<ShardManifest> priorShards = resumeFrom is null
            ? []
            // Sha256 is empty for resumed shards — re-streaming to recompute the digest
            // would defeat the point of checkpointing. The manifest serialiser writes an
            // empty string for these so downstream verifiers can flag and re-download.
            : [.. resumeFrom.CompletedShardObjectKeys.Select((key, idx) => new ShardManifest(idx, key, CompressedSizeBytes: 0, Sha256: []))];

        if (resumeFrom is not null)
        {
            LogResumingAssembly(logger, completion.RequestId, startFragmentIndex, startShardIndex);
        }
        else
        {
            await checkpointStore.SetAsync(
                completion.RequestId,
                completion.TenantId,
                new ExportAssemblyCheckpoint(
                    LastCompletedShardIndex: -1,
                    NextFragmentIndex: 0,
                    CompletedShardObjectKeys: []),
                cancellationToken).ConfigureAwait(false);
        }

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        List<string> emptyProviders = [];
        List<ExportManifestFragment> manifestFragments = [];

        string objectKeyPrefix = BuildObjectKeyPrefix(completion.RequestId);
        await using ShardingArchiveWriter writer = new(
            blobStoreProvider, PrivacyExportContainerBucket(opts), objectKeyPrefix, shardMaxBytes,
            startShardIndex: startShardIndex,
            priorShards: priorShards);

        DateTimeOffset hmacExpiryWindow = timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(opts.ExportTimeoutMinutes * 4);

        try
        {
            // The fragment list in the saga's completion event is deterministic per
            // RequestId, so a resumed run sees the same order. We iterate every fragment
            // to keep the manifest's emptyProviders / manifestFragments lists complete
            // (covering pre-resume work too), but only stream non-empty fragments at or
            // past startFragmentIndex into the writer.
            for (int i = 0; i < completion.Fragments.Count; i++)
            {
                ReceivedFragment fragment = completion.Fragments[i];

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

                if (i < startFragmentIndex)
                {
                    // Re-build manifest metadata for the fragment without re-streaming
                    // its bytes — the shard it lived in is already committed.
                    string priorEntryName = await ResolveEntryNameAsync(fragment, blobId, cancellationToken).ConfigureAwait(false);
                    manifestFragments.Add(new ExportManifestFragment(
                        fragment.ProviderName, priorEntryName, fragment.ContentType, fragment.BlobReferenceId));
                    continue;
                }

                if (!VerifyFragmentTag(completion, fragment, blobId, hmacExpiryWindow))
                {
                    LogIntegrityTagRejected(logger, fragment.ProviderName, completion.RequestId);
                    throw new InvalidOperationException(
                        $"HMAC integrity tag verification failed for provider '{fragment.ProviderName}' on request {completion.RequestId}.");
                }

                int shardsBefore = writer.Shards.Count;

                string entryName = await ResolveEntryNameAsync(fragment, blobId, cancellationToken).ConfigureAwait(false);
                await CopyFragmentToWriterAsync(writer, entryName, fragment, blobId, downloadTtl, httpClient, cancellationToken)
                    .ConfigureAwait(false);

                manifestFragments.Add(new ExportManifestFragment(
                    fragment.ProviderName, entryName, fragment.ContentType, fragment.BlobReferenceId));

                // AppendAsync rolls over BEFORE writing when the prior shard hit the cap,
                // so a growth in Shards.Count means the rolled-over shard is fully
                // committed to storage. Persist a checkpoint pointing at the fragment
                // currently in flight (this one): a re-dispatch will re-do this fragment
                // into a fresh shard, which is fine because the committed shards stay
                // addressable individually.
                if (writer.Shards.Count > shardsBefore)
                {
                    await checkpointStore.SetAsync(
                        completion.RequestId,
                        completion.TenantId,
                        new ExportAssemblyCheckpoint(
                            LastCompletedShardIndex: writer.Shards[^1].Index,
                            NextFragmentIndex: i,
                            CompletedShardObjectKeys: [.. writer.Shards.Select(s => s.ObjectKey)]),
                        cancellationToken).ConfigureAwait(false);
                }
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
        catch (Exception ex) when (ex is not OperationCanceledException
                                  && ex is not InvalidOperationException
                                  && ex is not PrivacyExportAssemblyException)
        {
            // Wrap unexpected transient failures so the Wolverine retry-with-cooldown
            // policy picks them up. InvalidOperationException (HMAC rejection,
            // malformed event) and PrivacyExportAssemblyException itself bypass the
            // wrapping — the former goes straight to DLQ via Wolverine's default
            // policy, the latter is already typed correctly.
            TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
            LogAssemblyFailed(logger, completion.RequestId, ex.GetType().Name);
            metrics.RecordArchiveAssembled(
                tenantId: completion.TenantId,
                status: "failed",
                completion.IsPartial,
                duration,
                completion.Regulation);
            throw new PrivacyExportAssemblyException(
                completion.RequestId,
                $"Privacy export {completion.RequestId} assembly failed: {ex.Message}",
                ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PrivacyExportAssemblyException)
        {
            // HMAC rejection / re-thrown PrivacyExportAssemblyException — preserve the
            // type so the dispatcher applies the right policy.
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
        // The signed-manifest envelope is a two-property object: `payload` carries
        // the canonical manifest content, `integrityTag` carries the HMAC over the
        // payload's UTF-8 bytes. Verifiers extract `payload`'s raw JSON text (via
        // JsonDocument.GetRawText / Utf8JsonReader) to recompute the HMAC — that's
        // why we serialise the payload separately and splice it in with
        // Utf8JsonWriter.WriteRawValue (no re-parse, byte-identical to what was
        // signed).
        var manifestPayload = new
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
                sha256 = s.Sha256.Length == 0 ? "" : Convert.ToHexStringLower(s.Sha256),
            }).ToArray(),
            fragments = manifestFragments,
        };

        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(manifestPayload, ManifestJsonOptions);
        string integrityTag = contentSigner.SignBytes(payloadBytes);

        byte[] payload = BuildSignedEnvelope(payloadBytes, integrityTag);

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

    private static byte[] BuildSignedEnvelope(byte[] payloadBytes, string integrityTag)
    {
        using MemoryStream ms = new(payloadBytes.Length + 128);
        using (Utf8JsonWriter writer = new(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("payload");
            // WriteRawValue writes payloadBytes verbatim — byte-identical to what
            // contentSigner.SignBytes saw. Without this, a re-parse + re-emit could
            // change whitespace or numeric formatting and break verification.
            writer.WriteRawValue(payloadBytes);
            writer.WriteString("integrityTag", integrityTag);
            writer.WriteEndObject();
        }
        return ms.ToArray();
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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export {RequestId}: resuming after a prior crashed run — skipping {SkipCount} fragments already committed, next shard index {NextShardIndex}")]
    private static partial void LogResumingAssembly(ILogger logger, Guid requestId, int skipCount, int nextShardIndex);

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

/// <summary>
/// Manifest entry describing a single fragment that contributed bytes to the
/// assembled archive — recorded inside the manifest sidecar uploaded by
/// <see cref="PrivacyExportAssemblyService"/>.
/// </summary>
/// <param name="ProviderName">Originating <c>IPrivacyDataProvider.ProviderName</c>.</param>
/// <param name="FileName">Resolved ZIP entry path inside the shard archives.</param>
/// <param name="ContentType">MIME type carried on the originating prepared event.</param>
/// <param name="BlobReferenceId">Source blob the bytes were streamed from.</param>
public sealed record ExportManifestFragment(
    string ProviderName,
    string FileName,
    string ContentType,
    BlobReference BlobReferenceId);

