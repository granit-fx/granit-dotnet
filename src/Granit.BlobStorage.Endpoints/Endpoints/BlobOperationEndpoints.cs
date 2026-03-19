using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Options;
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
            .WithSummary("Confirm a client-side upload — runs the validation pipeline");

        group.MapPost("/{id:guid}/download-url", GenerateDownloadUrlAsync)
            .WithName("GenerateBlobDownloadUrl")
            .WithSummary("Generate a pre-signed download URL");

        group.MapPost("/cleanup-orphans", CleanupOrphansAsync)
            .WithName("CleanupOrphanedBlobs")
            .WithSummary("Clean up orphaned blobs stuck in Pending/Uploading state");

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
