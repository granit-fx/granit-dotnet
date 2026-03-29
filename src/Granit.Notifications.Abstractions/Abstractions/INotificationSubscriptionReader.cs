using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Read operations for notification subscriptions (topic subscriptions + entity followers).
/// </summary>
public interface INotificationSubscriptionReader
{
    Task<IReadOnlyList<string>> GetSubscriberIdsAsync(string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationSubscription>> GetUserSubscriptionsAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);

    // Entity followers
    Task<IReadOnlyList<string>> GetEntityFollowerIdsAsync(string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationSubscription>> GetEntityFollowersAsync(string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<bool> IsFollowingEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default);
}
