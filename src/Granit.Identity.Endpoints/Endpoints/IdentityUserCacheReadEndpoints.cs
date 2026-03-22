using Granit.Identity.Endpoints.Dtos;
using Granit.Querying;
using Granit.Querying.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Read endpoints for the identity user cache (list, get, batch).
/// </summary>
internal static class IdentityUserCacheReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", SearchAsync)
            .WithName("SearchIdentityUserCache")
            .WithSummary("Searches the user cache by free-text term with pagination.")
            .WithDescription("Performs a free-text search across cached user fields (name, email, etc.) with pagination and sorting. Only searches the local cache — does not query the identity provider. Use sync endpoints to refresh stale data.")
            .Produces<PagedResult<IIdentityUser>>();

        group.MapGet("/{userId}", GetByIdAsync)
            .WithName("GetIdentityUserById")
            .WithSummary("Resolves a single user by external ID (cache-aside: fetches from provider if stale/missing).")
            .WithDescription("Looks up the user in the local cache first. If the entry is missing or stale, transparently fetches from the identity provider and updates the cache before returning. Returns 404 if the user does not exist in either the cache or the provider.")
            .Produces<IIdentityUser>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/batch", BatchResolveAsync)
            .WithName("BatchResolveIdentityUsers")
            .WithSummary("Resolves multiple user IDs to identity information in batch.")
            .WithDescription("Resolves a list of user IDs in a single request, using the cache-aside pattern. Unknown IDs are silently omitted from the result. Useful for enriching lists of entities with user display names without N+1 calls.")
            .Produces<IReadOnlyList<IIdentityUser>>();

        return group;
    }

    private static async Task<Ok<PagedResult<IIdentityUser>>> SearchAsync(
        [FromServices] IUserLookupService lookupService,
        BindableQueryRequest request,
        CancellationToken cancellationToken)
    {
        QueryRequest query = request.Value;
        int page = query.Page ?? 1;
        int pageSize = Math.Clamp(query.PageSize ?? QueryingDefaults.DefaultPageSize, 1, QueryingDefaults.MaxPageSize);

        PagedResult<IIdentityUser> result = await lookupService.SearchAsync(
            query.Search ?? "",
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<IIdentityUser>, NotFound>> GetByIdAsync(
        string userId,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        IIdentityUser? user = await lookupService.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(user);
    }

    private static async Task<Ok<IReadOnlyList<IIdentityUser>>> BatchResolveAsync(
        IdentityUserCacheBatchRequest request,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IIdentityUser> users = await lookupService.FindByIdsAsync(
            request.UserIds, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(users);
    }
}
