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
            .WithSummary("Get a blob descriptor by ID");

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
