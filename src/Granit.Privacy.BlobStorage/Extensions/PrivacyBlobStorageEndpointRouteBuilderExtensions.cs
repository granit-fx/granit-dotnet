using Granit.BlobStorage;
using Granit.Privacy.DataExport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.BlobStorage.Extensions;

/// <summary>
/// Endpoint extensions that require <c>Granit.BlobStorage</c>. Kept separate from
/// <c>Granit.Privacy.Endpoints</c> so that privacy endpoints (opt-out, deletion, agreements,
/// purposes, regulation) stay usable without pulling BlobStorage into every host.
/// </summary>
public static class PrivacyBlobStorageEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET /{prefix}/exports/{requestId}/download</c>. The handler resolves the tracker,
    /// verifies the caller owns the request, and 302-redirects to a presigned URL for the ZIP
    /// archive produced by <see cref="DataExport.ExportArchiveAssemblyHandler"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">Route prefix — typically mirrors <c>MapGranitPrivacy</c>. Default <c>"privacy"</c>.</param>
    public static RouteGroupBuilder MapGranitPrivacyExportDownload(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "privacy")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        RouteGroupBuilder group = endpoints
            .MapGroup(routePrefix)
            .RequireAuthorization()
            .WithTags("Privacy");

        group.MapGet("/exports/{requestId:guid}/download", HandleDownloadAsync)
             .WithName("DownloadPrivacyExportArchive")
             .WithSummary("Returns a presigned URL to download a completed export archive.")
             .WithDescription(
                 "Looks up the export request in the tracker, verifies the caller is the owner, "
                 + "and issues a 302 redirect to a short-lived presigned URL of the ZIP archive "
                 + "assembled by the archive-assembly handler. Returns 404 if the request does not "
                 + "exist or belongs to another user, and 409 if the archive is not ready "
                 + "(Pending, TimedOut with no fragments, SizeLimitExceeded).")
             .Produces(StatusCodes.Status302Found)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> HandleDownloadAsync(
        Guid requestId,
        HttpContext httpContext,
        [FromServices] IExportRequestTrackerReader tracker,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        Guid? callerId = TryGetUserId(httpContext);
        if (callerId is null)
        {
            return TypedResults.Problem(
                detail: "User is not authenticated or has no valid user ID.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        ExportRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null || status.UserId != callerId)
        {
            return TypedResults.Problem(
                detail: $"Export request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (status.State is not ExportRequestState.Completed and not ExportRequestState.PartiallyCompleted)
        {
            return TypedResults.Problem(
                detail: $"Export archive for request '{requestId}' is not available (state: {status.State}).",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (string.IsNullOrWhiteSpace(status.ArchiveBlobReferenceId) ||
            !Guid.TryParse(status.ArchiveBlobReferenceId, out Guid archiveBlobId))
        {
            return TypedResults.Problem(
                detail: $"Export archive for request '{requestId}' has no resolvable blob reference.",
                statusCode: StatusCodes.Status409Conflict);
        }

        PresignedDownloadUrl presigned = await blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            archiveBlobId,
            options: null,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Redirect(presigned.Url.ToString(), permanent: false, preserveMethod: false);
    }

    private static Guid? TryGetUserId(HttpContext httpContext)
    {
        System.Security.Claims.Claim? sub = httpContext.User.FindFirst("sub")
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return sub is not null && Guid.TryParse(sub.Value, out Guid userId) ? userId : null;
    }
}
