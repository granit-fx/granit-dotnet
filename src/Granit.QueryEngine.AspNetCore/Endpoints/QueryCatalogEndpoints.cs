using Granit.Entities;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace Granit.QueryEngine.AspNetCore.Endpoints;

/// <summary>
/// HTTP handler for the query-engine catalogue read surface.
/// </summary>
internal static class QueryCatalogEndpoints
{
    /// <summary>
    /// Localization-key prefix for a query's display label. The localizer (resolved from the
    /// definition's <see cref="IQueryDefinitionDescriptor.LocalizationResourceType"/>) is queried
    /// with <c>"Query:{Name}"</c>; a module opts in by adding that key to its own resource.
    /// </summary>
    private const string LabelKeyPrefix = "Query:";

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
        HttpContext httpContext,
        // Optional — a host without Granit.Localization wired still serves the catalogue,
        // with labels falling back to the raw query name.
        [FromServices] IStringLocalizerFactory? localizerFactory = null)
    {
        // Resolve each List-tagged endpoint's URL via LinkGenerator so route parameters
        // (e.g. /api/v{version:apiVersion}/patients) are substituted into the path.
        // The request's API version is passed EXPLICITLY: ambient-value reuse does not
        // reliably substitute {version:apiVersion} when linking to a *different* endpoint
        // than the current one, so it leaks as a `?version=1` query string
        // (e.g. /api/cms/pages?version=1 instead of /api/v1/cms/pages). An explicit route
        // value is always bound into the matching path segment. Reading RoutePattern.RawText
        // directly would expose the unsubstituted template and the frontend would 404.
        // Same mechanism as Granit.Entities' entity-discovery surface.
        RouteValueDictionary routeValues = new();
        if (httpContext.Request.RouteValues.TryGetValue("version", out object? version) && version is not null)
        {
            routeValues["version"] = version;
        }

        var routeIndex = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Meta = e.Metadata.GetMetadata<EntityEndpointMetadata>(),
                Name = e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
            })
            .Where(x => x.Meta is { Kind: EntityEndpointKind.List } && x.Name is not null)
            .Select(x => new { x.Meta, Path = linkGenerator.GetPathByName(httpContext, x.Name!, routeValues) })
            .Where(x => x.Path is not null)
            .GroupBy(x => x.Meta!.EntityType)
            .ToDictionary(g => g.Key, g => NormalizeRoutePath(g.First().Path!));

        return TypedResults.Ok(QueryCatalogProjection.Project(
            registry.GetAll(),
            routeIndex,
            descriptor => ResolveLabel(descriptor, localizerFactory)));
    }

    /// <summary>
    /// Resolves a query's display label by querying its localization resource with
    /// <c>"Query:{Name}"</c>, mirroring how the query engine resolves column labels. Falls back to
    /// the raw <see cref="IQueryDefinitionDescriptor.Name"/> when no factory, no resource, or no
    /// matching key is found — so the label is always present and localization stays opt-in.
    /// </summary>
    private static string ResolveLabel(
        IQueryDefinitionDescriptor descriptor,
        IStringLocalizerFactory? localizerFactory)
    {
        if (localizerFactory is not null && descriptor.LocalizationResourceType is { } resourceType)
        {
            IStringLocalizer localizer = localizerFactory.Create(resourceType);
            LocalizedString localized = localizer[LabelKeyPrefix + descriptor.Name];
            if (!localized.ResourceNotFound)
            {
                return localized.Value;
            }
        }

        return descriptor.Name;
    }

    /// <summary>
    /// Normalises a resolved list-endpoint path for use as a base path. Drops any query
    /// string (a base path must never carry one — the frontend appends <c>/meta</c>,
    /// <c>/{id}</c>, etc., so <c>/path?x=1/meta</c> would be malformed) and strips the
    /// trailing slash unless the path IS the root. Mirrors Granit.Entities' normalization.
    /// </summary>
    private static string NormalizeRoutePath(string path)
    {
        int query = path.IndexOf('?');
        if (query >= 0)
        {
            path = path[..query];
        }

        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
