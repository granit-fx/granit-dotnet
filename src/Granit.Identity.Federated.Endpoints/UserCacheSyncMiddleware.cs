using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Endpoints;

/// <summary>
/// ASP.NET Core middleware that syncs the current authenticated user's identity data
/// into the local cache from JWT claims on each request.
/// </summary>
/// <remarks>
/// Very lightweight: 1 SELECT + conditional UPSERT. Short-circuits if:
/// <list type="bullet">
///   <item>The request is not authenticated</item>
///   <item>The cached entry is fresh (within <see cref="UserCacheOptions.StalenessThreshold"/>)</item>
///   <item>Login-time sync is disabled via configuration</item>
/// </list>
/// No call to the identity provider — data comes directly from JWT claims.
/// </remarks>
internal sealed class UserCacheSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ICurrentUserService currentUserService,
        ICurrentTenant currentTenant,
        IUserCacheStore store,
        TimeProvider timeProvider,
        IOptions<UserCacheOptions> options,
        IIdentityProviderCapabilities capabilities)
    {
        // Skip sync when users are stored locally (OpenIddict / ASP.NET Core Identity).
        // LocalIdentity IS the source of truth — no cache needed.
        if (capabilities.IsLocalStore)
        {
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        if (options.Value.EnableLoginTimeSync
            && currentUserService.IsAuthenticated
            && currentUserService.UserId is { Length: > 0 } userId)
        {
            Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

            FederatedIdentity? existing = await store.FindByExternalIdAsync(userId, tenantId, httpContext.RequestAborted)
                .ConfigureAwait(false);

            DateTimeOffset now = timeProvider.GetUtcNow();

            if (existing is null || now - existing.LastSyncedAt >= options.Value.StalenessThreshold)
            {
                var entry = new FederatedIdentity
                {
                    ExternalUserId = userId,
                    Username = currentUserService.UserName,
                    Email = currentUserService.Email,
                    FirstName = currentUserService.FirstName,
                    LastName = currentUserService.LastName,
                    Enabled = true,
                    LastSyncedAt = now,
                    TenantId = tenantId
                };

                await store.UpsertAsync(entry, httpContext.RequestAborted).ConfigureAwait(false);
            }
        }

        await next(httpContext).ConfigureAwait(false);
    }
}
