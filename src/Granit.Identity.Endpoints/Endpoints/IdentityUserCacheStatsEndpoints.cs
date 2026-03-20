using Granit.Identity.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Stats endpoint for the identity user cache.
/// </summary>
internal static class IdentityUserCacheStatsEndpoints
{
    internal static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetIdentityUserCacheStats")
            .WithSummary("Returns cache statistics: total entries, stale count, oldest/newest sync timestamps.")
            .WithDescription("Returns aggregate statistics about the identity user cache: total number of cached entries, count of stale entries needing refresh, and the timestamp range of the oldest and newest synchronization. Useful for monitoring cache health and scheduling sync operations.")
            .Produces<IdentityUserCacheStatsResponse>();

        return group;
    }

    private static async Task<Ok<IdentityUserCacheStatsResponse>> GetStatsAsync(
        [FromServices] IUserCacheStats cacheStats,
        CancellationToken cancellationToken)
    {
        int total = await cacheStats.GetCountAsync(cancellationToken).ConfigureAwait(false);
        int stale = await cacheStats.GetStaleCountAsync(cancellationToken).ConfigureAwait(false);
        (DateTimeOffset? oldest, DateTimeOffset? newest) = await cacheStats.GetSyncRangeAsync(cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new IdentityUserCacheStatsResponse(total, stale, oldest, newest));
    }
}
