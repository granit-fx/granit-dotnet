using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage.Internal;

internal sealed class DefaultBlobContentReader(
    IBlobDescriptorReader reader,
    IBlobStoreProvider storeProvider,
    IBlobKeyStrategy keyStrategy) : IBlobContentReader
{
    public async Task<BlobContent?> ReadAsync(Guid blobId, CancellationToken cancellationToken = default)
    {
        BlobDescriptor? descriptor = await reader.FindAsync(blobId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null || descriptor.Status != BlobStatus.Valid)
        {
            return null;
        }

        string bucket = keyStrategy.ResolveBucketName(descriptor.ContainerName);
        Stream stream = await storeProvider.OpenReadAsync(bucket, descriptor.ObjectKey, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return new BlobContent(ms.ToArray(), descriptor.VerifiedContentType!, descriptor.OriginalFileName);
        }
    }
}
