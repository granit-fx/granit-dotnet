using Granit.Domain.ValueObjects;

namespace Granit.DataExchange.Internal;

/// <summary>
/// Default implementation of <see cref="IDataExchangeFileProvider"/>.
/// Throws <see cref="NotImplementedException"/> — requires a concrete file store.
/// </summary>
internal sealed class NullDataExchangeFileProvider : IDataExchangeFileProvider
{
    private const string Message =
        "Data exchange file storage requires a concrete IDataExchangeFileProvider. " +
        "Call builder.AddGranitDataExchangeBlobStorage() (Granit.DataExchange.BlobStorage) for production, " +
        "or services.AddInMemoryDataExchangeFileProvider() for tests and single-process CLI tools.";

    /// <inheritdoc/>
    public Task<Stream> OpenAsync(BlobReference blobReference, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<BlobReference> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<BlobReference> SaveAsync(
        string fileName,
        string contentType,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task DeleteAsync(BlobReference blobReference, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);
}
