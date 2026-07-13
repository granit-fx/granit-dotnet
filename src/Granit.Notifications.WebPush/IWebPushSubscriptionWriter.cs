namespace Granit.Notifications.WebPush;

/// <summary>Write operations for browser push subscriptions per user.</summary>
public interface IWebPushSubscriptionWriter
{
    /// <summary>Saves a push subscription for a user.</summary>
    Task SaveSubscriptionAsync(
        string userId, WebPushSubscriptionInfo subscription, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a push subscription by endpoint, scoped to its owning user so one tenant user
    /// can never delete another user's subscription (least privilege).
    /// </summary>
    Task RemoveSubscriptionAsync(
        string userId, string endpoint, Guid? tenantId, CancellationToken cancellationToken = default);
}
