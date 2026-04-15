namespace Granit.DataExchange.Internal;

/// <summary>
/// Default implementation of <see cref="IDataExchangeFileProvider"/>.
/// Throws <see cref="NotImplementedException"/> — the application must register a concrete implementation.
/// </summary>
internal sealed class NullDataExchangeFileProvider : IDataExchangeFileProvider
{
    /// <inheritdoc/>
    public Task<Stream> OpenAsync(string blobReference, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            "No IDataExchangeFileProvider is registered. " +
            "The host application must register an implementation that retrieves files from blob storage.");

    /// <inheritdoc/>
    public Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            "No IDataExchangeFileProvider is registered. " +
            "The host application must register an implementation that stores files to blob storage.");

    /// <inheritdoc/>
    public Task DeleteAsync(string blobReference, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            "No IDataExchangeFileProvider is registered. " +
            "The host application must register an implementation that deletes files from blob storage.");
}
