using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents;

namespace Granit.Documents.AssetMetadata.BackgroundJobs.Internal;

/// <summary>
/// Default <see cref="IAssetMetadataSourceFetcher"/> — issues a presigned download
/// URL via <see cref="IBlobStorage.CreateDownloadUrlAsync"/> against the
/// <see cref="DocumentBlobContainers.Documents"/> container, then HTTP GETs the bytes.
/// </summary>
internal sealed class HttpAssetMetadataSourceFetcher(
    IBlobStorage blobStorage,
    IHttpClientFactory httpClientFactory) : IAssetMetadataSourceFetcher
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for metadata source fetching.</summary>
    public const string HttpClientName = "granit.documents.asset-metadata.source";

    /// <inheritdoc />
    public async Task<Stream> OpenSourceAsync(Guid blobDescriptorId, CancellationToken cancellationToken = default)
    {
        PresignedDownloadUrl presigned = await blobStorage
            .CreateDownloadUrlAsync(
                DocumentBlobContainers.Documents,
                blobDescriptorId,
                options: null,
                cancellationToken)
            .ConfigureAwait(false);

        HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response = await http
            .GetAsync(presigned.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }
}
