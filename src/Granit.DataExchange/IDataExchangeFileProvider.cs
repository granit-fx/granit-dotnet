using Granit.Domain.ValueObjects;

namespace Granit.DataExchange;

/// <summary>
/// Provides file streams for data exchange jobs (import and export).
/// </summary>
/// <remarks>
/// A default in-memory implementation is registered automatically — suitable for
/// tests, CLI tools, and development. For production workloads, register
/// <c>Granit.DataExchange.BlobStorage</c> to delegate to the configured blob
/// storage provider (S3, Azure Blob, FileSystem, etc.).
/// </remarks>
public interface IDataExchangeFileProvider
{
    /// <summary>
    /// Opens a readable stream for the given blob reference.
    /// </summary>
    /// <param name="blobReference">The blob reference stored on the import/export job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream. The caller is responsible for disposing it.</returns>
    Task<Stream> OpenAsync(BlobReference blobReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a file and returns a blob reference for subsequent retrieval via <see cref="OpenAsync"/>.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="content">The file stream to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The blob reference produced by the provider.</returns>
    Task<BlobReference> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a file by writing directly into the provider's destination stream, avoiding a full
    /// in-memory buffer. <paramref name="writeAsync"/> receives the writable destination stream and
    /// must write the complete payload; the provider commits on success and discards on exception.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">MIME content type of the file being written.</param>
    /// <param name="writeAsync">Callback that writes the complete payload to the destination stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The blob reference produced by the provider.</returns>
    Task<BlobReference> SaveAsync(
        string fileName,
        string contentType,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously saved file by its blob reference.
    /// </summary>
    /// <param name="blobReference">The blob reference to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(BlobReference blobReference, CancellationToken cancellationToken = default);
}
