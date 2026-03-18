using System.Diagnostics;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Proxy.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.BlobStorage.Proxy.Internal;

/// <summary>
/// Minimal API handlers for proxied blob upload and download.
/// Token IS the authorization — endpoints are anonymous.
/// </summary>
internal static class ProxyEndpoints
{
    /// <summary>
    /// Handles <c>PUT /upload/{token}</c>: validates the ephemeral token,
    /// streams the request body directly to <see cref="IBlobStoreProvider.SaveAsync"/>.
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> HandleUploadAsync(
        string token,
        HttpContext context,
        [FromServices] IBlobProxyTokenStore tokenStore,
        [FromServices] IBlobStoreProvider storeProvider,
        CancellationToken cancellationToken)
    {
        using Activity? activity = BlobStorageProxyActivitySource.Source.StartActivity(BlobStorageProxyActivitySource.Upload);

        ProxyTokenEntry? entry = await tokenStore.ConsumeAsync(token, cancellationToken).ConfigureAwait(false);

        if (entry is null || entry.Type != ProxyTokenType.Upload)
        {
            return TypedResults.Problem(
                detail: "The upload token is invalid or has expired.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        activity?.SetTag(BlobStorageProxyActivitySource.TagBucket, entry.Bucket);
        activity?.SetTag(BlobStorageProxyActivitySource.TagObjectKey, entry.ObjectKey);
        activity?.SetTag(BlobStorageProxyActivitySource.TagContentType, entry.ContentType);

        // Validate Content-Length against the token's max bytes.
        if (context.Request.ContentLength is > 0 && context.Request.ContentLength > entry.MaxBytes)
        {
            return TypedResults.Problem(
                detail: "The uploaded file exceeds the maximum allowed size.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        // Validate Content-Type matches the declared type.
        string? requestContentType = context.Request.ContentType;
        if (entry.ContentType is not null &&
            !string.IsNullOrEmpty(requestContentType) &&
            !requestContentType.StartsWith(entry.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: "The Content-Type header does not match the expected type.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        // Stream directly from HttpRequest.Body — no RAM buffering.
        await storeProvider.SaveAsync(
            entry.Bucket,
            entry.ObjectKey,
            context.Request.Body,
            entry.ContentType ?? "application/octet-stream",
            cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET /download/{token}</c>: validates the ephemeral token,
    /// streams the blob content from <see cref="IBlobStoreProvider.OpenReadAsync"/>.
    /// </summary>
    internal static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> HandleDownloadAsync(
        string token,
        [FromServices] IBlobProxyTokenStore tokenStore,
        [FromServices] IBlobStoreProvider storeProvider,
        CancellationToken cancellationToken)
    {
        using Activity? activity = BlobStorageProxyActivitySource.Source.StartActivity(BlobStorageProxyActivitySource.Download);

        ProxyTokenEntry? entry = await tokenStore.ConsumeAsync(token, cancellationToken).ConfigureAwait(false);

        if (entry is null || entry.Type != ProxyTokenType.Download)
        {
            return TypedResults.Problem(
                detail: "The download token is invalid or has expired.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        activity?.SetTag(BlobStorageProxyActivitySource.TagBucket, entry.Bucket);
        activity?.SetTag(BlobStorageProxyActivitySource.TagObjectKey, entry.ObjectKey);

        Stream stream = await storeProvider.OpenReadAsync(
            entry.Bucket,
            entry.ObjectKey,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Stream(
            stream,
            contentType: "application/octet-stream",
            fileDownloadName: entry.DownloadFileName);
    }
}
