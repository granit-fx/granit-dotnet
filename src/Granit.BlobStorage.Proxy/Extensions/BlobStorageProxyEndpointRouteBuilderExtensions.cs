using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.Proxy.Internal;
using Granit.BlobStorage.Proxy.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Proxy.Extensions;

/// <summary>
/// Extension methods for mapping blob storage proxy endpoints.
/// </summary>
[ExcludeFromCodeCoverage]
public static class BlobStorageProxyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the blob storage proxy upload and download endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Endpoints are anonymous — the ephemeral token IS the authorization,
    /// mirroring the S3 pre-signed URL model.
    /// </para>
    /// <para>
    /// Routes: <c>PUT {RoutePrefix}/upload/{token}</c> and <c>GET {RoutePrefix}/download/{token}</c>.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitBlobProxyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ProxyBlobOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<ProxyBlobOptions>>().Value;

        RouteGroupBuilder group = endpoints
            .MapGroup(options.RoutePrefix)
            .WithTags("BlobProxy");

        group.MapPut("/upload/{token}", ProxyEndpoints.HandleUploadAsync)
            .WithName("BlobProxyUpload")
            .WithSummary("Proxied blob upload via ephemeral token.")
            .WithDescription("Uploads a blob using an ephemeral pre-signed token. The token IS the authorization (no bearer token required), mirroring the S3 pre-signed URL model. Returns 403 if the token is invalid or expired, 413 if the payload exceeds the configured size limit, and 415 if the content type is not allowed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .AllowAnonymous()
            .DisableAntiforgery();

        group.MapGet("/download/{token}", ProxyEndpoints.HandleDownloadAsync)
            .WithName("BlobProxyDownload")
            .WithSummary("Proxied blob download via ephemeral token.")
            .WithDescription("Downloads a blob using an ephemeral pre-signed token. The token IS the authorization (no bearer token required). Returns the blob content as a binary stream with the appropriate content type. Returns 403 if the token is invalid or expired.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AllowAnonymous();

        return endpoints;
    }
}
