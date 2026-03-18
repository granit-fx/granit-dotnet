using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Models;
using Granit.Querying;
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
            .WithDescription("Performs a free-text search across cached user fields (name, email, etc.) with pagination. Only searches the local cache — does not query the identity provider. Use sync endpoints to refresh stale data.");

        group.MapGet("/{userId}", GetByIdAsync)
            .WithName("GetIdentityUserById")
            .WithSummary("Resolves a single user by external ID (cache-aside: fetches from provider if stale/missing).")
            .WithDescription("Looks up the user in the local cache first. If the entry is missing or stale, transparently fetches from the identity provider and updates the cache before returning. Returns 404 if the user does not exist in either the cache or the provider.");

        group.MapPost("/batch", BatchResolveAsync)
            .WithName("BatchResolveIdentityUsers")
            .WithSummary("Resolves multiple user IDs to identity information in batch.")
            .WithDescription("Resolves a list of user IDs in a single request, using the cache-aside pattern. Unknown IDs are silently omitted from the result. Useful for enriching lists of entities with user display names without N+1 calls.");

        return group;
    }

    private static async Task<Ok<PagedResult<IdentityUser>>> SearchAsync(
        [FromServices] IUserLookupService lookupService,
        [AsParameters] IdentityUserCacheListRequest request,
        CancellationToken cancellationToken)
    {
        int clampedPage = Math.Max(request.Page, 1);
        int clampedPageSize = Math.Clamp(request.PageSize, 1, QueryingDefaults.MaxPageSize);

        PagedResult<IdentityUser> result = await lookupService.SearchAsync(
            request.Search ?? "",
            clampedPage,
            clampedPageSize,
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<IdentityUser>, NotFound>> GetByIdAsync(
        string userId,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        IdentityUser? user = await lookupService.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(user);
    }

    private static async Task<Ok<IReadOnlyList<IdentityUser>>> BatchResolveAsync(
        IdentityUserCacheBatchRequest request,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityUser> users = await lookupService.FindByIdsAsync(
            request.UserIds, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(users);
    }
}
