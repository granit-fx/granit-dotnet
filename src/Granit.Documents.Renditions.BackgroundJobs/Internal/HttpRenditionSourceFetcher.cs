using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;

namespace Granit.Documents.Renditions.BackgroundJobs.Internal;

/// <summary>
/// Default <see cref="IRenditionSourceFetcher"/> — issues a presigned download URL via
/// <see cref="IBlobStorage.CreateDownloadUrlAsync"/> against the
/// <see cref="DocumentBlobContainers.Documents"/> container, then HTTP GETs the bytes.
/// </summary>
internal sealed class HttpRenditionSourceFetcher(
    IBlobStorage blobStorage,
    IHttpClientFactory httpClientFactory) : IRenditionSourceFetcher
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for rendition source fetching.</summary>
    public const string HttpClientName = "granit.documents.renditions.source";

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
