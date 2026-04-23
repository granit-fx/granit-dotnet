using Granit.DataLookup.Endpoints.Dtos;
using Granit.DataLookup.Endpoints.Endpoints;
using Granit.DataLookup.Endpoints.Options;
using Granit.DataLookup.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.DataLookup.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit.DataLookup HTTP endpoints.
/// </summary>
public static class DataLookupEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the data-lookup endpoints onto <paramref name="endpoints"/>:
    /// <c>GET /</c> (manifest), <c>GET /{name}</c> (search), <c>GET /{name}/resolve</c>.
    /// </summary>
    /// <remarks>
    /// The coarse policy <c>DataLookup.Lookups.Read</c> is applied to the group. Per-source
    /// <c>RequiredPermission</c> values are enforced by the handlers after the source
    /// is resolved.
    /// </remarks>
    public static RouteGroupBuilder MapGranitDataLookups(
        this IEndpointRouteBuilder endpoints,
        Action<DataLookupEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        DataLookupEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(DataLookupPermissions.Lookups.Read);

        group.MapGet("/", LookupEndpointHandlers.GetManifest)
            .WithName("GetDataLookupManifest")
            .WithSummary("Returns the manifest of every registered lookup source.")
            .WithDescription(
                "Lists all lookups exposed by the host with their name, kind, required " +
                "permission, and scope keys. Frontend tooling uses this endpoint to " +
                "populate pickers and to hide lookups the user is not authorized to call.")
            .Produces<LookupManifestResponse>();

        group.MapGet("/{name}", LookupEndpointHandlers.SearchAsync)
            .WithName("SearchDataLookup")
            .WithSummary("Searches a lookup source for matching items.")
            .WithDescription(
                "Performs a paginated typeahead search against the lookup source identified " +
                "by the route parameter. Honors the Accept-Language header, the ambient tenant " +
                "context and any scope parameters declared by the source (scope.* query string).")
            .Produces<LookupResultResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{name}/resolve", LookupEndpointHandlers.ResolveAsync)
            .WithName("ResolveDataLookup")
            .WithSummary("Resolves a single lookup item by its value.")
            .WithDescription(
                "Used by clients to rehydrate a previously selected value into a " +
                "human-readable label. Returns 404 if the value is not present in the source.")
            .Produces<LookupItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
