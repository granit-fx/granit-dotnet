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
    /// Localization-key prefix used as the fallback label key when a query's target entity is not
    /// registered (so its already-translated <c>DisplayKey</c> cannot be reused). A module opts in
    /// by adding <c>"Query:{Name}"</c> to its own localization resource; the frontend resolves the
    /// key against the merged i18n bundle, exactly as it resolves entity display names.
    /// </summary>
    private const string LabelKeyPrefix = "Query:";

    /// <summary>
    /// Maps <c>GET /catalog</c> on the supplied route group. Returns every registered
    /// <see cref="QueryDefinition{TEntity}"/> projected through
    /// <see cref="QueryCatalogEntryResponse"/>, in the registry's stable order (module, then name).
    /// </summary>
    public static RouteGroupBuilder MapQueryCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/catalog", ListCatalogAsync)
            .WithName("ListQueryCatalog")
            .WithSummary("Lists every registered QueryDefinition.")
            .WithDescription(
                "Returns the full query catalogue surfaced by IQueryDefinitionRegistry so a "
                + "dashboard editor can offer a dropdown of queries instead of a free-text "
                + "queryName. Entries are ordered by owning module, then by name, so the editor "
                + "can group them under module headings. Each entry carries the wire identifier, "
                + "its owning module, a localization key for the label (the target entity's "
                + "display key when registered, else Query:{Name}), and "
                + "— when a MapGranitQuery route exposes it — the resolved base path of its list "
                + "endpoint. Registration and routing are decoupled: a query registered without a "
                + "mapped route is returned with a null base path rather than a forged URL.")
            .Produces<IReadOnlyList<QueryCatalogEntryResponse>>();

        return group;
    }

    private static Ok<IReadOnlyList<QueryCatalogEntryResponse>> ListCatalogAsync(
        [FromServices] IQueryDefinitionRegistry registry,
        [FromServices] EndpointDataSource endpointDataSource,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] IEnumerable<IEntityDefinitionDescriptor> entityDescriptors,
        HttpContext httpContext)
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
        RouteValueDictionary routeValues = [];
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

        Dictionary<Type, string> entityDisplayKeys = BuildEntityDisplayKeyIndex(entityDescriptors);

        return TypedResults.Ok(QueryCatalogProjection.Project(
            registry.GetAll(),
            routeIndex,
            descriptor => ResolveLabelKey(descriptor, entityDisplayKeys)));
    }

    /// <summary>
    /// Returns the localization key the frontend resolves for a query's dropdown label: the target
    /// entity's <c>DisplayKey</c> when that entity is registered — reusing its already-translated
    /// name — otherwise the <c>"Query:{Name}"</c> convention key. The server emits a key, never a
    /// resolved string; translation happens client-side, consistent with entity discovery.
    /// </summary>
    private static string ResolveLabelKey(
        IQueryDefinitionDescriptor descriptor,
        Dictionary<Type, string> entityDisplayKeys) =>
        entityDisplayKeys.TryGetValue(descriptor.EntityType, out string? displayKey)
            ? displayKey
            : LabelKeyPrefix + descriptor.Name;

    /// <summary>
    /// Indexes registered entity definitions by CLR type → <c>DisplayKey</c>, skipping entities
    /// that declare no display key. A query whose entity is absent falls back to its own label key.
    /// </summary>
    private static Dictionary<Type, string> BuildEntityDisplayKeyIndex(
        IEnumerable<IEntityDefinitionDescriptor> entityDescriptors) =>
        entityDescriptors
            .Where(d => d.Descriptor.DisplayKey is not null)
            .GroupBy(d => d.EntityType)
            .ToDictionary(g => g.Key, g => g.First().Descriptor.DisplayKey!);

    /// <summary>
    /// Normalises a resolved list-endpoint path for use as a base path. Drops any query
    /// string (a base path must never carry one — the frontend appends <c>/meta</c>,
    /// <c>/{id}</c>, etc., so <c>/path?x=1/meta</c> would be malformed) and strips the
    /// trailing slash unless the path IS the root. Mirrors Granit.Entities' normalization.
    /// </summary>
    private static string NormalizeRoutePath(string path)
    {
        int query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            path = path[..query];
        }

        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
