using Granit.Events;
using Granit.Identity.Events;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Internal;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Handlers;

/// <summary>
/// Wolverine handler that processes identity provider user events and updates the local cache.
/// </summary>
internal sealed partial class IdentityUserEventHandler(
    IIdentityProvider identityProvider,
    IUserCacheStore store,
    ILocalEventBus localEventBus,
    IDistributedEventBus distributedEventBus,
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
    /// Handles a user deleted event by hard-deleting the cache entry (RGPD Art. 17).
    /// </summary>
    public async Task HandleAsync(IdentityUserDeletedEto @event, CancellationToken cancellationToken)
    {
        await store.DeleteByExternalIdAsync(@event.UserId, @event.TenantId, cancellationToken)
            .ConfigureAwait(false);

        await localEventBus.PublishAsync(
            new UserCacheEntryErasedEvent(@event.UserId, @event.TenantId), cancellationToken)
            .ConfigureAwait(false);

        LogUserCacheDeleted(@event.UserId);
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
            return;
        }

        var entry = new UserCacheEntry
        {
            ExternalUserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Enabled = user.Enabled,
            LastSyncedAt = timeProvider.GetUtcNow()
        };

        await store.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new UserCacheSyncedEto(user.UserId, entry.TenantId, entry.LastSyncedAt), cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} not found in identity provider during cache sync")]
    private partial void LogUserNotFoundInProvider(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] User cache entry updated via {Source} for user {UserId}")]
    private partial void LogUserCacheUpdated(string userId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] RGPD: user cache entry deleted via webhook for user {UserId}")]
    private partial void LogUserCacheDeleted(string userId);
}
