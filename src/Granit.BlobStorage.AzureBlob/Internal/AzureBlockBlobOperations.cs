using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Granit.BlobStorage.AzureBlob.Internal;

/// <summary>
/// Default <see cref="IAzureBlockBlobOperations"/> backed by a real
/// <see cref="BlockBlobClient"/>. Production path for the assembly job;
/// <see cref="AzureBlockBlobMultipartWriteStream"/> resolves it via the
/// <see cref="AzureBlobClient"/> override.
/// </summary>
internal sealed class AzureBlockBlobOperations(BlockBlobClient client) : IAzureBlockBlobOperations
{
    public Task StageBlockAsync(string blockId, Stream content, CancellationToken cancellationToken) =>
        client.StageBlockAsync(blockId, content, cancellationToken: cancellationToken);

    public async Task CommitBlockListAsync(IReadOnlyList<string> blockIds, string contentType, CancellationToken cancellationToken)
    {
        CommitBlockListOptions options = new()
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        };
        await client.CommitBlockListAsync(blockIds, options, cancellationToken).ConfigureAwait(false);
    }
}
