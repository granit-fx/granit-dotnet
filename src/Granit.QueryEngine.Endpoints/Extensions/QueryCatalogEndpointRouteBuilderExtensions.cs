using Granit.QueryEngine.Endpoints.Endpoints;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.QueryEngine.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering the query-engine catalogue endpoint.
/// </summary>
public static class QueryCatalogEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET {prefix}/catalog</c>, exposing every registered
    /// <see cref="QueryDefinition{TEntity}"/> so a dashboard editor can offer a dropdown of
    /// queries instead of a free-text <c>queryName</c>. Independent of
    /// <see cref="QueryEndpointRouteBuilderExtensions.MapGranitQuery{TEntity}(IEndpointRouteBuilder, string, System.Action{Options.QueryEndpointOptions})"/>:
    /// a query registered via <c>AddQueryDefinition</c> appears in the catalogue even when no
    /// route exposes it (with a <c>null</c> base path).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Optional route prefix for the catalogue group. Defaults to empty.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitQueryCatalog(
        this IEndpointRouteBuilder endpoints,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGranitGroup(prefix)
            .WithTags("Query Engine");

        // Authenticated-only gate: the catalogue exposes query identifiers and base paths
        // (metadata, not row data), mirroring the per-query list endpoints' default. The
        // descriptors carry no PermissionGroup, so there is nothing to filter per-item on.
        group.RequireAuthorization();

        group.MapQueryCatalogEndpoints();

        return group;
    }
}
