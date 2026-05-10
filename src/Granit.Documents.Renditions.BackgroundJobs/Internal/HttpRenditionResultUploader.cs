using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Guids;

namespace Granit.Documents.Renditions.BackgroundJobs.Internal;

/// <summary>
/// Default <see cref="IRenditionResultUploader"/> — runs the presigned upload dance
/// (<see cref="IBlobStorage.InitiateUploadAsync"/> → HTTP PUT → <see cref="IBlobStorage.ConfirmUploadAsync"/>)
/// against the <see cref="DocumentRenditionContainers.Renditions"/> container.
/// </summary>
internal sealed class HttpRenditionResultUploader(
    IBlobStorage blobStorage,
    IHttpClientFactory httpClientFactory,
    IGuidGenerator guidGenerator) : IRenditionResultUploader
{
    /// <summary>Name of the <see cref="HttpClient"/> used to PUT rendition bytes.</summary>
    public const string HttpClientName = "granit.documents.renditions.upload";

    /// <inheritdoc />
    public async Task<Guid> UploadAsync(RenditionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            DocumentRenditionContainers.Renditions,
            new BlobUploadRequest(
                FileName: $"{guidGenerator.Create():N}",
                ContentType: result.ContentType,
                MaxAllowedBytes: result.Content.Length),
            cancellationToken).ConfigureAwait(false);

        HttpClient http = httpClientFactory.CreateClient(HttpClientName);
        using ByteArrayContent content = new(result.Content);
        content.Headers.ContentType = new MediaTypeHeaderValue(result.ContentType);
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }

        using HttpRequestMessage request = new(new HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using HttpResponseMessage response = await http
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await blobStorage.ConfirmUploadAsync(
            DocumentRenditionContainers.Renditions,
            ticket.BlobId,
            cancellationToken).ConfigureAwait(false);

        return ticket.BlobId;
    }
}
