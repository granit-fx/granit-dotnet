using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Dtos;
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
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<BlobDescriptorResponse>, NotFound>> GetByIdAsync(
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
            return TypedResults.NotFound();
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
