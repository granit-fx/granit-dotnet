namespace Granit.BlobStorage.GoogleCloud.Internal;

/// <summary>
/// Thin testing seam over the three GCS resumable-upload HTTP calls that
/// <see cref="GcsResumableMultipartWriteStream"/> needs: session initiate, chunk
/// upload, and abort. Keeps the stream decoupled from the raw HTTP protocol so
/// unit tests can mock with NSubstitute instead of standing up an
/// <see cref="HttpClient"/> mock.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chunk size discipline.</b> Every chunk except the final one MUST be a
/// multiple of 256 KiB (262,144 bytes) — GCS rejects mid-stream chunks of any
/// other size. <see cref="GcsResumableMultipartWriteStream"/> enforces this by
/// only flushing when the in-memory buffer reaches the configured chunk size,
/// which is itself constrained to be a multiple of 256 KiB.
/// </para>
/// <para>
/// <b>Session URI.</b> A resumable upload session is identified by an opaque URI
/// returned in the <c>Location</c> header of the initiate response. The same
/// URI is the target of every chunk PUT and the eventual DELETE on abort. It
/// expires after one week per the GCS service contract — the storage bucket's
/// <c>abort_incomplete_multipart_upload</c> lifecycle rule is the safety net
/// for sessions whose <see cref="AbortSessionAsync"/> never fires.
/// </para>
/// </remarks>
internal interface IGcsResumableUploadOperations
{
    /// <summary>
    /// Initiates a resumable upload session and returns the per-session URI used
    /// for subsequent chunk uploads.
    /// </summary>
    Task<Uri> InitiateSessionAsync(string bucket, string objectKey, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a single chunk against an in-progress session. Non-final chunks
    /// pass <paramref name="totalSize"/> as <see langword="null"/>; the final
    /// chunk passes the total object size, which commits the upload.
    /// </summary>
    Task UploadChunkAsync(
        Uri sessionUri,
        ReadOnlyMemory<byte> chunk,
        long offset,
        long? totalSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Aborts a resumable upload session. Best-effort — the bucket lifecycle
    /// policy is the safety net when the network drops.
    /// </summary>
    Task AbortSessionAsync(Uri sessionUri, CancellationToken cancellationToken);
}
