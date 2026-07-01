using Granit.Authorization.Extensions;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.BlobStorage.Endpoints.Endpoints;

internal static class BlobReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetBlobDescriptor")
            .WithSummary("Returns a blob descriptor by its unique identifier.")
            .WithDescription(
                "Fetches the full metadata of a blob including its status, content type, size, "
                + "and validation results. The containerName query parameter is required. "
                + "Returns 404 if the blob does not exist in the specified container.")
            .Produces<BlobDescriptorResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowHostAccess();

        group.MapGet("/{id:guid}/download", DownloadByIdAsync)
            .WithName("DownloadBlob")
            .WithSummary("Redirects to a fresh pre-signed URL for direct blob download.")
            .WithDescription(
                "Resolves a Valid blob by its identifier within the current tenant and issues a 302 "
                + "redirect to a freshly generated pre-signed download URL. Designed for direct "
                + "<img src>/browser consumption over the cookie/BFF session: honouring the module's "
                + "Direct-to-Cloud contract, the application server never streams the bytes. The "
                + "container is resolved server-side from the identifier, so no query parameter is "
                + "required. Only blobs in the Valid state are served; returns 404 for missing, "
                + "pending, rejected, or deleted blobs.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Download)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> DownloadByIdAsync(
        Guid id,
        HttpContext httpContext,
        [FromServices] IBlobDescriptorReader descriptorReader,
        [FromServices] IBlobStorage blobStorage,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        BlobDescriptor? descriptor = await descriptorReader
            .FindAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (descriptor is null || descriptor.Status != BlobStatus.Valid)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        PresignedDownloadUrl url = await blobStorage
            .CreateDownloadUrlAsync(descriptor.ContainerName, id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        ApplyRedirectCaching(httpContext, url.ExpiresAt, clock);

        return TypedResults.Redirect(url.Url.ToString());
    }

    // The redirect target is a short-lived pre-signed URL, so the 302 may be cached by the browser
    // only until shortly before that URL expires — a 30s safety margin guards against clock skew and
    // in-flight latency. This lets repeated <img> renders reuse the same target (and its cached bytes)
    // without ever following a stale link.
    private static void ApplyRedirectCaching(HttpContext httpContext, DateTimeOffset expiresAt, IClock clock)
    {
        TimeSpan reusableFor = expiresAt - clock.Now - TimeSpan.FromSeconds(30);

        httpContext.Response.Headers.CacheControl = reusableFor > TimeSpan.Zero
            ? $"private, max-age={(int)reusableFor.TotalSeconds}"
            : "no-store";
    }

    private static async Task<Results<Ok<BlobDescriptorResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromQuery] string containerName,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        BlobDescriptor? descriptor = await blobStorage
            .GetDescriptorAsync(containerName, id, cancellationToken)
            .ConfigureAwait(false);

        if (descriptor is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(MapToResponse(descriptor));
    }

    internal static BlobDescriptorResponse MapToResponse(BlobDescriptor d) =>
        new(
            d.Id,
            d.ContainerName,
            d.OriginalFileName,
            d.DeclaredContentType,
            d.VerifiedContentType,
            d.MaxAllowedBytes,
            d.SizeBytes,
            d.Status,
            d.RejectionReason,
            d.DeletionReason,
            d.CreatedAt,
            d.ValidatedAt,
            d.DeletedAt);
}
