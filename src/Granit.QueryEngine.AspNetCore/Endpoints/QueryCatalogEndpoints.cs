using Granit.Entities;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.QueryEngine.AspNetCore.Endpoints;

/// <summary>
/// HTTP handler for the query-engine catalogue read surface.
/// </summary>
internal static class QueryCatalogEndpoints
{
    /// <summary>
    /// Maps <c>GET /catalog</c> on the supplied route group. Returns every registered
    /// <see cref="QueryDefinition{TEntity}"/> projected through
    /// <see cref="QueryCatalogEntryResponse"/>, in the registry's stable (name-ordinal) order.
    /// </summary>
    public static RouteGroupBuilder MapQueryCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/catalog", ListCatalogAsync)
            .WithName("ListQueryCatalog")
            .WithSummary("Lists every registered QueryDefinition.")
            .WithDescription(
                "Returns the full query catalogue surfaced by IQueryDefinitionRegistry so a "
                + "dashboard editor can offer a dropdown of queries instead of a free-text "
                + "queryName. Each entry carries the wire identifier and, when a MapGranitQuery "
                + "route exposes it, the resolved base path of its list endpoint. Registration "
                + "and routing are decoupled: a query registered without a mapped route is "
                + "returned with a null base path rather than a forged URL.")
            .Produces<IReadOnlyList<QueryCatalogEntryResponse>>();

        return group;
    }

    private static Ok<IReadOnlyList<QueryCatalogEntryResponse>> ListCatalogAsync(
        [FromServices] IQueryDefinitionRegistry registry,
        [FromServices] EndpointDataSource endpointDataSource,
        [FromServices] LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        // Resolve each List-tagged endpoint's URL via LinkGenerator so route parameters
        // (e.g. /api/v{version:apiVersion}/patients) are substituted using the current
        // request's ambient route values. Reading RoutePattern.RawText directly would
        // expose the unsubstituted template and the frontend would 404. Same mechanism
        // as Granit.Entities' entity-discovery surface.
        var routeIndex = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Meta = e.Metadata.GetMetadata<EntityEndpointMetadata>(),
                Name = e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
            })
            .Where(x => x.Meta is { Kind: EntityEndpointKind.List } && x.Name is not null)
            .Select(x => new { x.Meta, Path = linkGenerator.GetPathByName(httpContext, x.Name!) })
            .Where(x => x.Path is not null)
            .GroupBy(x => x.Meta!.EntityType)
            .ToDictionary(g => g.Key, g => NormalizeRoutePath(g.First().Path!));

        return TypedResults.Ok(QueryCatalogProjection.Project(registry.GetAll(), routeIndex));
    }

    /// <summary>
    /// Strips the trailing slash from a resolved path unless it IS the root. The frontend
    /// appends <c>/meta</c>, <c>/{id}</c>, etc. on top of the base, so a trailing slash would
    /// yield <c>/path//meta</c> on the wire. Mirrors Granit.Entities' normalization.
    /// </summary>
    private static string NormalizeRoutePath(string path) =>
        path.Length > 1 ? path.TrimEnd('/') : path;
}
