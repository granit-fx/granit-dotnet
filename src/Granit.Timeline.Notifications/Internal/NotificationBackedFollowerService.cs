using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Timeline.Abstractions;

namespace Granit.Timeline.Notifications.Internal;

/// <summary>
/// Implements <see cref="ITimelineFollowerService"/> by delegating to
/// <see cref="INotificationSubscriptionReader"/> and <see cref="INotificationSubscriptionWriter"/>
/// entity follower methods.
/// </summary>
internal sealed class NotificationBackedFollowerService(
    INotificationSubscriptionReader subscriptionReader,
    INotificationSubscriptionWriter subscriptionWriter,
    ICurrentTenant currentTenant) : ITimelineFollowerService
{
    /// <inheritdoc/>
    public Task FollowAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        subscriptionWriter.FollowEntityAsync(userId, entityType, entityId, TenantId, cancellationToken);

    /// <inheritdoc/>
    public Task UnfollowAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        subscriptionWriter.UnfollowEntityAsync(userId, entityType, entityId, TenantId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetFollowerIdsAsync(string entityType, string entityId, CancellationToken cancellationToken = default) =>
        subscriptionReader.GetEntityFollowerIdsAsync(entityType, entityId, TenantId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsFollowingAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        subscriptionReader.IsFollowingEntityAsync(userId, entityType, entityId, TenantId, cancellationToken);

    private Guid? TenantId => currentTenant.IsAvailable ? currentTenant.Id : null;
}
