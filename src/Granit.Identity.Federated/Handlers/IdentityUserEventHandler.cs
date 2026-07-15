using Granit.Events;
using Granit.Identity.Events;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.RateLimiting;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Handlers;

/// <summary>
/// Wolverine handler that processes identity provider user events and updates the local cache.
/// </summary>
internal sealed partial class IdentityUserEventHandler(
    IIdentityProvider identityProvider,
    IIdentityProviderCapabilities providerCapabilities,
    IUserCacheStore store,
    IFederatedIdentityWriter writer,
    ICurrentTenant currentTenant,
    ILocalEventBus localEventBus,
    IDistributedEventBus distributedEventBus,
    IUserSyncFailureRateLimiter syncFailureRateLimiter,
    TimeProvider timeProvider,
    ILogger<IdentityUserEventHandler> logger)
{
    /// <summary>
    /// Handles a user created/updated event (from webhook) by fetching the user from
    /// the identity provider and upserting the cache entry.
    /// </summary>
    public async Task HandleAsync(IdentityUserUpdatedEto @event, CancellationToken cancellationToken)
    {
        await SyncUserCacheAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        LogUserCacheUpdated(@event.UserId, "webhook");
    }

    /// <summary>
    /// Handles a user deleted event by hard-deleting the cache entry (GDPR Art. 17).
    /// A null <see cref="IdentityUserDeletedEto.TenantId"/> erases across all tenants.
    /// </summary>
    public async Task HandleAsync(IdentityUserDeletedEto @event, CancellationToken cancellationToken)
    {
        // Distributed dispatch carries no ambient tenant — establish the event's scope so the
        // multi-tenant query filter exposes the row the delete predicate targets. The null
        // (all-tenants) case bypasses the filter inside the store, per the Eto contract.
        using IDisposable _ = currentTenant.Change(@event.TenantId);

        int deleted = await store.DeleteByExternalIdAsync(@event.UserId, @event.TenantId, cancellationToken)
            .ConfigureAwait(false);

        await localEventBus.PublishAsync(
            new FederatedIdentityErasedEvent(@event.UserId, @event.TenantId), cancellationToken)
            .ConfigureAwait(false);

        LogUserCacheDeleted(@event.UserId, deleted);
    }

    // ──── Domain event handlers (provider-triggered) ────

    /// <summary>
    /// Handles a user created event by syncing the new user into the local cache.
    /// </summary>
    public async Task HandleAsync(IdentityUserCreatedEto @event, CancellationToken cancellationToken)
    {
        await SyncUserCacheAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        LogUserCacheUpdated(@event.UserId, "create");
    }

    /// <summary>
    /// Handles a user profile updated event by refreshing the cache entry.
    /// </summary>
    public async Task HandleAsync(IdentityUserProfileUpdatedEto @event, CancellationToken cancellationToken)
    {
        await SyncUserCacheAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        LogUserCacheUpdated(@event.UserId, "profile-update");
    }

    /// <summary>
    /// Handles a user enabled/disabled event by refreshing the cache entry.
    /// </summary>
    public async Task HandleAsync(IdentityUserEnabledChangedEto @event, CancellationToken cancellationToken)
    {
        await SyncUserCacheAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        LogUserCacheUpdated(@event.UserId, "enabled-change");
    }

    // ──── Shared helpers ────

    private async Task SyncUserCacheAsync(string userId, CancellationToken cancellationToken)
    {
        IIdentityUser? user = await identityProvider.GetUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            LogUserNotFoundInProvider(userId);
            await EmitSyncFailedAsync(
                userId,
                reason: "User not found in identity provider during cache sync.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        // Route through the shared writer so the webhook path gets the same ADR-051 joint
        // hydration (canonical User + aligned Id/UserId) as the cache-aside path — the old
        // inline upsert inserted a FederatedIdentity with UserId = Guid.Empty and no User row.
        FederatedIdentity entry = await writer.SyncAsync(user, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new UserCacheSyncedEto(user.UserId, entry.TenantId, entry.LastSyncedAt), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EmitSyncFailedAsync(string userId, string reason, CancellationToken cancellationToken)
    {
        string providerName = providerCapabilities.ProviderName;
        if (!syncFailureRateLimiter.TryAcquire(userId, providerName))
        {
            // Same (user, provider) failure already emitted within the cool-off
            // window — keep the log line above for short-term continuity, skip
            // the integration event to avoid log-storm noise.
            LogSyncFailureSuppressed(userId, providerName);
            return;
        }

        await distributedEventBus.PublishAsync(
            new IdentityUserSyncFailedEto(
                userId,
                providerName,
                reason,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} not found in identity provider during cache sync")]
    private partial void LogUserNotFoundInProvider(string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suppressed IdentityUserSyncFailedEto emission for user {UserId} on provider {ProviderName} (cool-off window active)")]
    private partial void LogSyncFailureSuppressed(string userId, string providerName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] User cache entry updated via {Source} for user {UserId}")]
    private partial void LogUserCacheUpdated(string userId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] GDPR: {DeletedCount} user cache entries deleted via webhook for user {UserId}")]
    private partial void LogUserCacheDeleted(string userId, int deletedCount);
}
