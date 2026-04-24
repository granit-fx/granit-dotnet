using Granit.Catalog.Domain;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Catalog.Endpoints.Permissions;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Catalog.Endpoints.Endpoints;

internal static class ProductExternalMappingEndpoints
{
    internal static RouteGroupBuilder MapProductExternalMappingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/products/{id:guid}/external-mappings", AddExternalMappingAsync)
            .WithName("AddCatalogProductExternalMapping")
            .WithSummary("Adds an external provider mapping (Stripe / Avalara / Odoo / ...) to a product.")
            .WithDescription(
                "Allowed in any lifecycle state. Each (provider, externalId) pair must be globally unique "
                + "(one external identifier maps to exactly one product).")
            .WithMetadata(new IdempotentAttribute())
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        group.MapDelete("/products/{id:guid}/external-mappings/{mappingId:guid}", RemoveExternalMappingAsync)
            .WithName("RemoveCatalogProductExternalMapping")
            .WithSummary("Removes an external provider mapping from a product.")
            .WithDescription("Returns 404 if either the product or the mapping does not exist.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        return group;
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> AddExternalMappingAsync(
        Guid id,
        AddProductExternalMappingRequest request,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var mapping = ProductExternalMapping.Create(
            guidGenerator.Create(),
            request.ProviderName,
            request.ExternalId);
        product.AddExternalMapping(mapping);

        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ProductResponse.FromEntity(product));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveExternalMappingAsync(
        Guid id,
        Guid mappingId,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (!product.RemoveExternalMapping(mappingId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
