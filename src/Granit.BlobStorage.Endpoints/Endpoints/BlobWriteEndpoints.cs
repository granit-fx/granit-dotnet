using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.Http.Idempotency.Attributes;
using Granit.Http.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.BlobStorage.Endpoints.Endpoints;

internal static class BlobWriteEndpoints
{
    internal static RouteGroupBuilder MapWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/upload", InitiateUploadAsync)
            .WithName("InitiateBlobUpload")
            .WithSummary("Initiates a direct-to-cloud upload and returns a pre-signed URL.")
            .WithDescription(
                "Creates a new blob descriptor and generates a pre-signed URL for direct client-side upload. "
                + "The response includes the upload URL, required HTTP headers, and expiration time. "
                + "After uploading, call the confirm endpoint to trigger the validation pipeline.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<BlobUploadInitiateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(BlobStorageRateLimitPolicies.Upload);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteBlob")
            .WithSummary("Deletes a blob using crypto-shredding.")
            .WithDescription(
                "Permanently removes the blob content from storage via crypto-shredding. "
                + "The blob descriptor and audit trail are retained with the provided deletion reason. "
                + "A containerName and deletionReason must be supplied in the request body.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<BlobUploadInitiateResponse>> InitiateUploadAsync(
        BlobUploadInitiateRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        BlobUploadRequest uploadRequest = new(request.FileName, request.ContentType, request.SizeBytes);

        PresignedUploadTicket ticket = await blobStorage
            .InitiateUploadAsync(request.ContainerName, uploadRequest, cancellationToken)
            .ConfigureAwait(false);

        BlobUploadInitiateResponse response = new(
            ticket.BlobId,
            ticket.UploadUrl.ToString(),
            ticket.HttpMethod,
            ticket.ExpiresAt,
            ticket.RequiredHeaders);

        return TypedResults.Created($"/{ticket.BlobId}", response);
    }

    private static async Task<NoContent> DeleteAsync(
        Guid id,
        [FromBody] BlobDeleteRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        await blobStorage
            .DeleteAsync(request.ContainerName, id, request.DeletionReason, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
