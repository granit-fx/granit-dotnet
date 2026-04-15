namespace Granit.DataExchange;

/// <summary>
/// Provides file streams for data exchange jobs (import and export).
/// The application registers an implementation that retrieves or stores files
/// via its blob storage or any other file system.
/// </summary>
/// <remarks>
/// A concrete implementation is typically registered in the host application,
/// bridging <c>IBlobStorage</c> or a local file system to the data exchange pipeline.
/// </remarks>
public interface IDataExchangeFileProvider
{
    /// <summary>
    /// Opens a readable stream for the given blob reference.
    /// </summary>
    /// <param name="blobReference">The blob reference stored on the import/export job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream. The caller is responsible for disposing it.</returns>
    Task<Stream> OpenAsync(string blobReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a file and returns a blob reference for subsequent retrieval via <see cref="OpenAsync"/>.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="content">The file stream to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The blob reference string.</returns>
    Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously saved file by its blob reference.
    /// </summary>
    /// <param name="blobReference">The blob reference to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string blobReference, CancellationToken cancellationToken = default);
}
