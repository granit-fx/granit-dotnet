using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Sync endpoints for forcing refresh from the identity provider.
/// </summary>
internal static class IdentityUserCacheSyncEndpoints
{
    internal static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sync", SyncAsync)
            .WithName("SyncIdentityUsers")
            .WithSummary("Forces refresh of specific users from the identity provider.")
            .WithDescription("Fetches the latest data for the specified user IDs from the identity provider and updates the cache. Returns the refreshed user records. Users not found in the provider are silently skipped.")
            .Produces<IReadOnlyList<IdentityUser>>();

        group.MapPost("/sync-all", SyncAllAsync)
            .WithName("SyncAllIdentityUsers")
            .WithSummary("Full sync — fetches all users from the identity provider and upserts the cache.")
            .WithDescription("Fetches every user from the identity provider and upserts the entire cache. This is an expensive operation — use sparingly (e.g., nightly scheduled job or initial setup). Returns the count of synchronized entries.")
            .Produces<IdentityUserCacheSyncAllResponse>();

        group.MapPost("/sync-stale", SyncStaleAsync)
            .WithName("SyncStaleIdentityUsers")
            .WithSummary("Incremental sync — refreshes only stale cache entries.")
            .WithDescription("Refreshes only cache entries whose last sync timestamp exceeds the configured staleness threshold. More efficient than a full sync for routine maintenance. Returns the count of refreshed entries.")
            .Produces<IdentityUserCacheSyncStaleResponse>();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentityUser>>> SyncAsync(
        IdentityUserCacheSyncRequest request,
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        var results = new List<IdentityUser>();

        foreach (string userId in request.UserIds)
        {
            IdentityUser? refreshed = await lookupService.RefreshByIdAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            if (refreshed is not null)
            {
                results.Add(refreshed);
            }
        }

        return TypedResults.Ok<IReadOnlyList<IdentityUser>>(results);
    }

    private static async Task<Ok<IdentityUserCacheSyncAllResponse>> SyncAllAsync(
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        int synced = await lookupService.RefreshAllAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new IdentityUserCacheSyncAllResponse(synced));
    }

    private static async Task<Ok<IdentityUserCacheSyncStaleResponse>> SyncStaleAsync(
        [FromServices] IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        int refreshed = await lookupService.RefreshStaleAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new IdentityUserCacheSyncStaleResponse(refreshed));
    }
}

/// <summary>Response for the sync-all endpoint.</summary>
/// <param name="SyncedCount">Number of users synchronized.</param>
internal sealed record IdentityUserCacheSyncAllResponse(int SyncedCount);

/// <summary>Response for the sync-stale endpoint.</summary>
/// <param name="RefreshedCount">Number of stale entries refreshed.</param>
internal sealed record IdentityUserCacheSyncStaleResponse(int RefreshedCount);
