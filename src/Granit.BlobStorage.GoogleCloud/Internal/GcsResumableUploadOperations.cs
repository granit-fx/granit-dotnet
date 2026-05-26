using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using Google.Cloud.Storage.V1;

namespace Granit.BlobStorage.GoogleCloud.Internal;

/// <summary>
/// Default <see cref="IGcsResumableUploadOperations"/> wired against the
/// <see cref="StorageClient"/>'s underlying authenticated
/// <see cref="HttpClient"/>. Production path for the assembly job;
/// <see cref="GcsResumableMultipartWriteStream"/> resolves it via the
/// <see cref="GoogleCloudBlobClient"/> override.
/// </summary>
/// <remarks>
/// The implementation speaks raw HTTP because <see cref="StorageClient"/> only
/// exposes resumable uploads bound to a known-length source stream — incompatible
/// with the forward-only, length-unknown stream surface required by
/// <see cref="MultipartWriteStream"/>.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class GcsResumableUploadOperations(StorageClient storageClient) : IGcsResumableUploadOperations
{
    private const string UploadEndpoint = "https://storage.googleapis.com/upload/storage/v1/b/";

    // 308 is the GCS "chunk accepted, more bytes expected" status. .NET's HttpStatusCode
    // enum doesn't name it (it's a non-standard reuse of "Permanent Redirect"), so refer
    // to it numerically.
    private const int ResumeIncompleteStatusCode = 308;

    private readonly HttpClient _httpClient = storageClient.Service.HttpClient;

    public async Task<Uri> InitiateSessionAsync(string bucket, string objectKey, string contentType, CancellationToken cancellationToken)
    {
        string url = $"{UploadEndpoint}{Uri.EscapeDataString(bucket)}/o?uploadType=resumable&name={Uri.EscapeDataString(objectKey)}";

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.Add("X-Upload-Content-Type", contentType);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Headers.Location is null)
        {
            throw new InvalidOperationException(
                "GCS resumable upload initiation did not return a session URI in the Location header.");
        }

        return response.Headers.Location;
    }

    public async Task UploadChunkAsync(
        Uri sessionUri,
        ReadOnlyMemory<byte> chunk,
        long offset,
        long? totalSize,
        CancellationToken cancellationToken)
    {
        string totalSegment = totalSize?.ToString(CultureInfo.InvariantCulture) ?? "*";
        string contentRange = chunk.Length == 0
            ? $"bytes */{totalSegment}"
            : string.Create(CultureInfo.InvariantCulture, $"bytes {offset}-{offset + chunk.Length - 1}/{totalSegment}");

        using HttpRequestMessage request = new(HttpMethod.Put, sessionUri);
        ByteArrayContent content = new(chunk.ToArray());
        content.Headers.ContentLength = chunk.Length;
        if (!content.Headers.TryAddWithoutValidation("Content-Range", contentRange))
        {
            throw new InvalidOperationException($"Failed to set Content-Range header on GCS chunk upload: '{contentRange}'.");
        }
        request.Content = content;

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK
            && response.StatusCode != HttpStatusCode.Created
            && (int)response.StatusCode != ResumeIncompleteStatusCode)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task AbortSessionAsync(Uri sessionUri, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, sessionUri);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            // 204 No Content is the documented success status; 499 (non-standard) also indicates aborted.
        }
        catch
        {
            // Best-effort abort. The bucket lifecycle policy is the safety net.
        }
    }
}
