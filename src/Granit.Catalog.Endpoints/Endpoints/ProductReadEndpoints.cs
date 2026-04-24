using Granit.Catalog.Domain;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Catalog.Endpoints.Permissions;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Catalog.Endpoints.Endpoints;

internal static class ProductReadEndpoints
{
    internal static RouteGroupBuilder MapProductReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/products", ListPublishedProductsAsync)
            .WithName("ListPublishedProducts")
            .WithSummary("Returns all Published products in the catalog.")
            .WithDescription(
                "Lists products available for use by Subscriptions and Metering. "
                + "Draft and Archived products are excluded — use the QueryEngine endpoint "
                + "(when available) for paginated, filterable, status-aware listings.")
            .Produces<IReadOnlyList<ProductResponse>>()
            .RequireAuthorization(CatalogPermissions.Products.Read);

        group.MapGet("/products/{id:guid}", GetProductByIdAsync)
            .WithName("GetCatalogProductById")
            .WithSummary("Returns a product by ID (any lifecycle status).")
            .WithDescription("Returns the full product details including external mappings and metadata.")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(CatalogPermissions.Products.Read);

        group.MapGet("/products/by-sku/{sku}", GetProductBySkuAsync)
            .WithName("GetCatalogProductBySku")
            .WithSummary("Returns a product by SKU (any lifecycle status).")
            .WithDescription("Useful for SKU-based reverse lookups during Stripe / Avalara / Odoo integration syncs.")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(CatalogPermissions.Products.Read);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<ProductResponse>>> ListPublishedProductsAsync(
        [FromServices] IProductReader productReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Product> products = await productReader
            .GetByStatusAsync(WorkflowLifecycleStatus.Published, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ProductResponse> response = products
            .Select(ProductResponse.FromEntity).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> GetProductByIdAsync(
        Guid id,
        [FromServices] IProductReader productReader,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader
            .GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return product is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(ProductResponse.FromEntity(product));
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> GetProductBySkuAsync(
        string sku,
        [FromServices] IProductReader productReader,
        CancellationToken cancellationToken)
    {
        Product? product = await productReader
            .GetBySkuAsync(sku, cancellationToken).ConfigureAwait(false);

        return product is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(ProductResponse.FromEntity(product));
    }
}
