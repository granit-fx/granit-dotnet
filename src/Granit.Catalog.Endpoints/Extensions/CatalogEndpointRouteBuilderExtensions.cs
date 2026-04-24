using Granit.Catalog.Domain;
using Granit.Catalog.Endpoints.Endpoints;
using Granit.Catalog.Endpoints.Options;
using Granit.Catalog.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Catalog.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering catalog administration endpoints.
/// </summary>
public static class CatalogEndpointRouteBuilderExtensions
{
    /// <summary>Maps the catalog administration endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="CatalogEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitCatalog(
        this IEndpointRouteBuilder endpoints,
        Action<CatalogEndpointsOptions>? configure = null)
    {
        CatalogEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        RouteGroupBuilder productsGroup = group.MapGranitGroup(string.Empty)
            .WithTags(options.ProductsTagName);
        productsGroup.MapProductReadEndpoints();
        productsGroup.MapProductWriteEndpoints();
        productsGroup.MapProductExternalMappingEndpoints();

        // QueryEngine admin grid — mounted on a dedicated sub-path to avoid colliding
        // with the business endpoints above (/products returns only Published; the grid
        // here exposes filter / sort / paginate / export across all lifecycle statuses).
        // Convention mirrors Granit.Metering.Endpoints (/metering/meter-definitions).
        group.MapGranitGroup("product-records")
            .MapGranitQuery<Product>()
            .RequireAuthorization(CatalogPermissions.Products.Read);

        return group;
    }
}
