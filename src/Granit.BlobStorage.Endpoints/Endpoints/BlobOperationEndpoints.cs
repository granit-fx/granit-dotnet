using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.BlobStorage.Options;
using Granit.Http.Idempotency.Attributes;
using Granit.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.BlobStorage.Endpoints.Endpoints;

internal static class BlobOperationEndpoints
{
    internal static RouteGroupBuilder MapOperationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/confirm", ConfirmUploadAsync)
            .WithName("ConfirmBlobUpload")
            .WithSummary("Confirms a client-side upload and runs the validation pipeline.")
            .WithDescription(
                "Triggers content-type verification and size validation on an uploaded blob. "
                + "The response includes the validation result: verified content type, actual size, "
                + "and an optional rejection reason if the blob failed validation.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<BlobConfirmUploadResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Upload);

        group.MapPost("/{id:guid}/download-url", GenerateDownloadUrlAsync)
            .WithName("GenerateBlobDownloadUrl")
            .WithSummary("Generates a time-limited pre-signed download URL.")
            .WithDescription(
                "Creates a pre-signed URL for direct client-side download of the blob content. "
                + "An optional custom file name can be specified to override the Content-Disposition header. "
                + "The URL expires after the provider-configured duration.")
            .Produces<BlobDownloadUrlResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Download);

        group.MapDelete("/{id:guid}/pending", CancelPendingUploadAsync)
            .WithName("CancelPendingBlobUpload")
            .WithSummary("Cancels a Pending upload whose presigned PUT failed client-side.")
            .WithDescription(
                "Short-circuits the orphan-cleanup window by transitioning a Pending blob directly to Rejected. "
                + "Intended to be called by the client when its PUT to the presigned URL returns 4xx/5xx, "
                + "so the descriptor does not linger visible for up to BlobStorageOptions.OrphanCleanupAge. "
                + "Returns 409 Conflict if the blob has already left Pending state.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Upload);

        group.MapPost("/cleanup-orphans", CleanupOrphansAsync)
            .WithName("CleanupOrphanedBlobs")
            .WithSummary("Cleans up orphaned blobs stuck in Pending or Uploading state.")
            .WithDescription(
                "Scans for blobs that never completed the upload/confirm cycle and deletes them. "
                + "Returns the number of orphaned blobs removed. "
                + "Typically called on a schedule or via the admin dashboard.")
            .Produces<BlobCleanupOrphansResponse>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Admin);

        return group;
    }

    private static async Task<Ok<BlobConfirmUploadResponse>> ConfirmUploadAsync(
        Guid id,
        BlobConfirmUploadRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        BlobConfirmationResult result = await blobStorage
            .ConfirmUploadAsync(request.ContainerName, id, cancellationToken)
            .ConfigureAwait(false);

        BlobConfirmUploadResponse response = new(
            id,
            result.IsValid,
            result.Status,
            result.VerifiedContentType,
            result.SizeBytes,
            result.RejectionReason);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<BlobDownloadUrlResponse>> GenerateDownloadUrlAsync(
        Guid id,
        BlobDownloadUrlRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        DownloadUrlOptions? options = request.FileName is not null
            ? new DownloadUrlOptions(DownloadFileName: request.FileName)
            : null;

        PresignedDownloadUrl url = await blobStorage
            .CreateDownloadUrlAsync(request.ContainerName, id, options, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new BlobDownloadUrlResponse(url.Url.ToString(), url.ExpiresAt));
    }

    private static async Task<NoContent> CancelPendingUploadAsync(
        Guid id,
        BlobCancelPendingRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        await blobStorage
            .CancelPendingUploadAsync(request.ContainerName, id, request.Reason, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BlobCleanupOrphansResponse>> CleanupOrphansAsync(
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        int cleaned = await blobStorage
            .CleanupOrphansAsync(cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new BlobCleanupOrphansResponse(cleaned));
    }
}
