using Granit.BlobStorage;
using Granit.BlobStorage.Internal;
using Granit.DataExchange.BlobStorage.Options;
using Granit.Guids;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.BlobStorage.Internal;

/// <summary>
/// <see cref="IDataExchangeFileProvider"/> implementation backed by <see cref="IBlobStoreProvider"/>.
/// Stores and retrieves data exchange files (import uploads, export outputs) using the
/// configured blob storage provider (S3, Azure Blob, FileSystem, etc.).
/// </summary>
internal sealed class BlobStorageFileProvider(
    IBlobStoreProvider storeProvider,
    IBlobKeyStrategy keyStrategy,
    IGuidGenerator guidGenerator,
    IOptions<DataExchangeBlobStorageOptions> options) : IDataExchangeFileProvider
{
    /// <inheritdoc/>
    public async Task<Stream> OpenAsync(string blobReference, CancellationToken cancellationToken = default)
    {
        string bucket = keyStrategy.ResolveBucketName(options.Value.ContainerName);
        return await storeProvider.OpenReadAsync(bucket, blobReference, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        string objectKey = keyStrategy.BuildObjectKey(options.Value.ContainerName, guidGenerator.Create());
        string bucket = keyStrategy.ResolveBucketName(options.Value.ContainerName);

        await storeProvider.SaveAsync(
            bucket,
            objectKey,
            content,
            options.Value.DefaultContentType,
            cancellationToken).ConfigureAwait(false);

        return objectKey;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string blobReference, CancellationToken cancellationToken = default)
    {
        string bucket = keyStrategy.ResolveBucketName(options.Value.ContainerName);
        await storeProvider.DeleteAsync(bucket, blobReference, cancellationToken)
            .ConfigureAwait(false);
    }
}
