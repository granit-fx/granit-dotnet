namespace Granit.BlobStorage.Internal;

/// <summary>
/// Low-level blob storage operations. Implemented by every provider package
/// (S3, Azure Blob, FileSystem, Database).
/// </summary>
internal interface IBlobStoreProvider
{
    /// <summary>Writes blob content to the store.</summary>
    /// <param name="bucket">Physical bucket / base path resolved by <see cref="IBlobKeyStrategy"/>.</param>
    /// <param name="objectKey">Full object key including tenant prefix.</param>
    /// <param name="content">
    /// Blob content stream. Implementations MUST stream end-to-end without
    /// buffering the entire payload in memory (critical for large files).
    /// The caller retains ownership of the stream.
    /// </param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        string bucket,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the full blob content for reading.</summary>
    /// <returns>
    /// A readable <see cref="Stream"/>. The caller is responsible for disposing it.
    /// Implementations MUST return a streaming response, not a fully buffered copy.
    /// </returns>
    Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>Physically deletes the blob content from the store.</summary>
    Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the actual size of the blob in bytes.</summary>
    Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a partial read stream starting from byte 0 up to <paramref name="byteCount"/> bytes.
    /// Used by <c>MagicBytesValidator</c> for content-type detection without buffering the full file.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a streaming write surface whose final size is not known in advance —
    /// used by content producers that write incrementally (ZIP shards, archive
    /// builders) and cannot buffer the entire payload in memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller writes bytes via the returned <see cref="MultipartWriteStream"/>
    /// and finalises with either
    /// <see cref="MultipartWriteStream.CompleteAsync"/> (commit the blob) or
    /// <see cref="MultipartWriteStream.AbortAsync"/> (discard). Disposing without
    /// calling either is treated as an abort — partial bytes never reach the blob.
    /// </para>
    /// <para>
    /// The default implementation buffers writes to a
    /// <c>FileOptions.DeleteOnClose</c> temp file and ships the whole content via
    /// a single <see cref="SaveAsync"/> on
    /// <see cref="MultipartWriteStream.CompleteAsync"/>. Correct for every
    /// provider but not memory-optimal — providers with native multipart support
    /// (S3 InitiateMultipartUpload, Azure StageBlock, GCS resumable upload)
    /// override this method to stream parts directly without the temp-file hop.
    /// </para>
    /// </remarks>
    Task<MultipartWriteStream> OpenWriteMultipartAsync(
        string bucket,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        return Task.FromResult<MultipartWriteStream>(
            new BufferedMultipartWriteStream(this, bucket, objectKey, contentType));
    }
}
