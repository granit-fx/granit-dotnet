using Granit.BlobStorage.Endpoints.Dtos;
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
            .WithSummary("Initiate a direct-to-cloud upload and get a pre-signed URL");

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteBlob")
            .WithSummary("Delete a blob (crypto-shredding — audit record retained)");

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
        BlobDeleteRequest request,
        [FromServices] IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        await blobStorage
            .DeleteAsync(request.ContainerName, id, request.DeletionReason, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
