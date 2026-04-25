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

internal static class ProductWriteEndpoints
{
    internal static RouteGroupBuilder MapProductWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/products", CreateProductAsync)
            .WithName("CreateCatalogProduct")
            .WithSummary("Creates a new product in Draft status.")
            .WithDescription("Creates a product that can be configured (metadata, external mappings) before publishing.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        group.MapPut("/products/{id:guid}", UpdateProductAsync)
            .WithName("UpdateCatalogProduct")
            .WithSummary("Updates editable fields of a Draft product.")
            .WithDescription(
                "Only Draft products can be updated. SKU and Type are immutable post-creation — "
                + "to change them, archive the product and create a new one.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        group.MapPut("/products/{id:guid}/metadata", UpdateProductMetadataAsync)
            .WithName("UpdateCatalogProductMetadata")
            .WithSummary("Replaces the product metadata dictionary.")
            .WithDescription(
                "Allowed in any lifecycle state — metadata may need to flow even for Published "
                + "products (e.g., adjusting Stripe sync attributes). MUST NOT contain PII.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        group.MapPost("/products/{id:guid}/publish", PublishProductAsync)
            .WithName("PublishCatalogProduct")
            .WithSummary("Publishes a Draft product, making it available for use.")
            .WithDescription("Transitions Draft → Published. This unlocks the product for MeterDefinition.ProductId and PlanPrice.ProductId references.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        group.MapPost("/products/{id:guid}/archive", ArchiveProductAsync)
            .WithName("ArchiveCatalogProduct")
            .WithSummary("Archives a Published product.")
            .WithDescription("Existing references (meters, plan prices) are unaffected; the product becomes unavailable for new attachments.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(CatalogPermissions.Products.Manage);

        return group;
    }

    private static async Task<Results<Created<ProductResponse>, ValidationProblem>> CreateProductAsync(
        ProductCreateRequest request,
        [FromServices] IProductWriter productWriter,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProductType>(request.Type, ignoreCase: true, out ProductType type))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Type"] = [$"Invalid product type: {request.Type}"],
            });
        }

        var product = Product.Create(
            guidGenerator.Create(),
            request.Sku,
            request.Name,
            type,
            request.Unit,
            request.Description);

        await productWriter.AddAsync(product, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/catalog/products/{product.Id}", ProductResponse.FromEntity(product));
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> UpdateProductAsync(
        Guid id,
        ProductUpdateRequest request,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            product.Update(request.Name, request.Description, request.Unit);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ProductResponse.FromEntity(product));
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> UpdateProductMetadataAsync(
        Guid id,
        UpdateProductMetadataRequest request,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        product.ReplaceMetadata(request.Metadata);
        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ProductResponse.FromEntity(product));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PublishProductAsync(
        Guid id,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            product.Publish();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveProductAsync(
        Guid id,
        [FromServices] IProductReader productReader,
        [FromServices] IProductWriter productWriter,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            product.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await productWriter.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
