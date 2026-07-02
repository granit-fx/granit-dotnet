using System.Text.Json;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Internal;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Exceptions;
using Granit.Privacy.DataExport.Security;

namespace Granit.Privacy.BlobStorage.DataExport;

/// <summary>
/// Default <see cref="IPrivacyExportDownloadResolver"/> backed by the BlobStorage
/// descriptor registry (for the manifest sidecar) plus
/// <see cref="IBlobStoreProvider.OpenReadAsync"/> (for the shard archives, which
/// the assembly job writes directly without descriptor entries).
/// </summary>
/// <remarks>
/// The manifest sidecar is stored AES-256-GCM encrypted (application-layer, GDPR Art. 32 —
/// see <see cref="IExportContentEncryptor"/>). This resolver is the single decrypt seam:
/// it reads the ciphertext blob, decrypts it in-process behind the download endpoint's
/// subject-identity + step-up gate, and hands the plaintext to the caller. Decrypt
/// authenticates the GCM tag first, so a tampered manifest throws before any plaintext is
/// materialised. Shard ZIPs stream through unchanged — their bytes are the (already
/// HMAC-verified) provider fragments, not the SAR index; extending encryption to shards is
/// a follow-up if the threat model demands it.
/// </remarks>
internal sealed class BlobBackedPrivacyExportDownloadResolver(
    IBlobStorage blobStorage,
    IBlobStoreProvider blobStoreProvider,
    IExportContentEncryptor contentEncryptor,
    IExportRequestTrackerReader trackerReader) : IPrivacyExportDownloadResolver
{
    public async Task<PrivacyExportManifestSummary> ReadManifestSummaryAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        ManifestLookup lookup = await ReadManifestAsync(requestId, cancellationToken).ConfigureAwait(false);
        return new PrivacyExportManifestSummary(lookup.Shards.Count, lookup.ManifestObjectKey);
    }

    public async Task<PrivacyExportDownloadPayload> OpenManifestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        // Skip the JSON parse — we don't need shard metadata to return the manifest
        // stream itself, only the descriptor's object key. Saves one blob read per
        // manifest download vs routing through ReadManifestAsync.
        BlobDescriptor descriptor = await ResolveManifestDescriptorAsync(requestId, cancellationToken).ConfigureAwait(false);
        byte[] plaintext = await ReadAndDecryptManifestAsync(descriptor, cancellationToken).ConfigureAwait(false);

        // The subject receives decrypted plaintext JSON over the authenticated, step-up-gated
        // BFF channel — the ciphertext and the key stay server-side. Buffered (not streamed
        // from the blob) because decrypt needs the whole authenticated ciphertext in hand.
        return new PrivacyExportDownloadPayload(
            Content: new MemoryStream(plaintext, writable: false),
            ContentType: "application/json",
            FileName: $"personal-data-export-{requestId}-manifest.json",
            LengthBytes: plaintext.Length);
    }

    public async Task<PrivacyExportDownloadPayload> OpenShardAsync(
        Guid requestId,
        int shardIndex,
        CancellationToken cancellationToken)
    {
        ManifestLookup lookup = await ReadManifestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (shardIndex < 0 || shardIndex >= lookup.Shards.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shardIndex),
                $"Shard index {shardIndex} is out of range for export {requestId} (manifest declares {lookup.Shards.Count} shard(s)).");
        }

        ManifestShard shard = lookup.Shards[shardIndex];
        Stream stream = await blobStoreProvider
            .OpenReadAsync(PrivacyExportContainerNames.FragmentContainer, shard.ObjectKey, cancellationToken)
            .ConfigureAwait(false);

        return new PrivacyExportDownloadPayload(
            Content: stream,
            ContentType: "application/zip",
            FileName: $"personal-data-export-{requestId}-{shardIndex:D3}.zip",
            LengthBytes: shard.CompressedSizeBytes > 0 ? shard.CompressedSizeBytes : null);
    }

    private async Task<BlobDescriptor> ResolveManifestDescriptorAsync(Guid requestId, CancellationToken cancellationToken)
    {
        ExportRequestStatus? status = await trackerReader.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (status?.ArchiveBlobReferenceId is null
            || !Guid.TryParse(status.ArchiveBlobReferenceId.Value, out Guid manifestBlobId))
        {
            throw new PrivacyExportNotReadyException(requestId);
        }

        BlobDescriptor? descriptor = await blobStorage
            .GetDescriptorAsync(PrivacyExportContainerNames.FragmentContainer, manifestBlobId, cancellationToken)
            .ConfigureAwait(false);

        return descriptor ?? throw new PrivacyExportNotReadyException(requestId);
    }

    private async Task<ManifestLookup> ReadManifestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        BlobDescriptor descriptor = await ResolveManifestDescriptorAsync(requestId, cancellationToken).ConfigureAwait(false);
        byte[] plaintext = await ReadAndDecryptManifestAsync(descriptor, cancellationToken).ConfigureAwait(false);

        ManifestEnvelope envelope = JsonSerializer.Deserialize<ManifestEnvelope>(plaintext, ManifestJsonOptions)
            ?? throw new PrivacyExportNotReadyException(requestId);

        return new ManifestLookup(descriptor.ObjectKey, envelope.Payload?.Shards ?? []);
    }

    // Reads the stored manifest ciphertext and returns the decrypted plaintext bytes. The
    // manifest is uploaded AES-256-GCM encrypted by the assembly service; Decrypt validates
    // the authentication tag first, so a tampered ciphertext throws CryptographicException
    // before any plaintext is produced. The whole ciphertext must be buffered because AEAD
    // decryption is not a streaming operation over an untrusted source.
    private async Task<byte[]> ReadAndDecryptManifestAsync(BlobDescriptor descriptor, CancellationToken cancellationToken)
    {
        await using Stream ciphertextStream = await blobStoreProvider
            .OpenReadAsync(PrivacyExportContainerNames.FragmentContainer, descriptor.ObjectKey, cancellationToken)
            .ConfigureAwait(false);

        using MemoryStream buffer = new();
        await ciphertextStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return contentEncryptor.Decrypt(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record ManifestLookup(string ManifestObjectKey, IReadOnlyList<ManifestShard> Shards);

    // Signed-manifest envelope: { "payload": { schemaVersion, ..., shards: [...] }, "integrityTag": "v1:..." }.
    // The resolver only needs `shards` from inside the payload — it doesn't re-verify the
    // HMAC, that's the verifier's job (a future endpoint or client-side CLI). All we
    // care about here is routing shardIndex → objectKey.
    private sealed record ManifestEnvelope(ManifestPayload? Payload);

    private sealed record ManifestPayload(IReadOnlyList<ManifestShard>? Shards);

    private sealed record ManifestShard(int Index, string ObjectKey, long CompressedSizeBytes);
}
