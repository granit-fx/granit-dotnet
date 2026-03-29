namespace Granit.Notifications.Abstractions;

/// <summary>
/// Write operations for notification subscriptions (topic subscriptions + entity followers).
/// </summary>
public interface INotificationSubscriptionWriter
{
    Task SubscribeAsync(string userId, string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string userId, string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default);

    // Entity followers
    Task FollowEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task UnfollowEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default);
}
