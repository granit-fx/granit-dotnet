using Microsoft.AspNetCore.Builder;

namespace Granit.Identity.Federated.Endpoints.Extensions;

/// <summary>
/// Extension methods for adding identity user cache middleware to the ASP.NET Core pipeline.
/// </summary>
public static class IdentityFederatedEndpointsApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="UserCacheSyncMiddleware"/> to the pipeline.
    /// This middleware automatically syncs the current authenticated user's identity data
    /// into the local cache from JWT claims on each request when the cached entry is stale or missing.
    /// </summary>
    /// <remarks>
    /// Must be placed after authentication and tenant resolution middleware.
    /// </remarks>
    public static IApplicationBuilder UseGranitIdentityUserCacheSync(this IApplicationBuilder app) =>
        app.UseMiddleware<UserCacheSyncMiddleware>();
}
