namespace Granit.Notifications.WebPush;

/// <summary>Read operations for browser push subscriptions per user.</summary>
public interface IWebPushSubscriptionReader
{
    /// <summary>Gets all push subscriptions for a user.</summary>
    Task<IReadOnlyList<WebPushSubscriptionInfo>> GetSubscriptionsAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
