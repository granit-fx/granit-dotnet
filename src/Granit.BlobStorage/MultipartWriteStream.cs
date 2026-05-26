namespace Granit.BlobStorage;

/// <summary>
/// Streaming write surface for a blob whose final size is not known in advance —
/// used by callers that produce content incrementally (ZIP shards, archive
/// builders) and cannot buffer the entire payload in memory.
/// </summary>
/// <remarks>
/// <para>
/// Callers write bytes via the inherited <see cref="Stream"/> API and finalise
/// the blob by calling either <see cref="CompleteAsync"/> (commit the written
/// bytes to the blob) or <see cref="AbortAsync"/> (discard them). Disposing
/// the stream without calling either is treated as an abort — partial bytes
/// are never silently committed.
/// </para>
/// <para>
/// The default implementation
/// (<c>Internal.BufferedMultipartWriteStream</c>) buffers writes to a
/// FileOptions.DeleteOnClose temp file and performs a single
/// <see cref="Internal.IBlobStoreProvider.SaveAsync"/> on
/// <see cref="CompleteAsync"/>. Native multipart implementations (S3
/// InitiateMultipartUpload, Azure Block Blob StageBlock, GCS resumable upload)
/// override the contract for memory-efficiency on multi-GB blobs.
/// </para>
/// </remarks>
public abstract class MultipartWriteStream : Stream, IAsyncDisposable
{
    /// <summary>
    /// Finalises the multipart write. After this call returns, the blob is
    /// readable via the standard <see cref="IBlobStorage"/> APIs. Calling
    /// <see cref="CompleteAsync"/> twice is a no-op; calling it after
    /// <see cref="AbortAsync"/> throws.
    /// </summary>
    public abstract Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards every byte written so far. Idempotent; safe to call after
    /// <see cref="CompleteAsync"/> (in which case it's a no-op — the blob has
    /// already been committed). Implementations clean up provider-side state
    /// (S3 AbortMultipartUpload, temp files, …).
    /// </summary>
    public abstract Task AbortAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously releases resources held by the stream. Implementations
    /// that have not been committed via <see cref="CompleteAsync"/> or
    /// explicitly aborted via <see cref="AbortAsync"/> MUST abort here so
    /// partial bytes never reach the blob.
    /// </summary>
    public override abstract ValueTask DisposeAsync();
}
