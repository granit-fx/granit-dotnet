namespace Granit.Webhooks.Domain;

/// <summary>
/// Lifecycle status of a webhook subscription.
/// </summary>
public enum WebhookSubscriptionStatus
{
    /// <summary>Subscription is active and receives delivery attempts.</summary>
    Active,

    /// <summary>
    /// Subscription is temporarily suspended (e.g., after repeated HTTP failures).
    /// Can be reactivated by an operator.
    /// </summary>
    Suspended,

    /// <summary>
    /// Subscription has been permanently deactivated and will never receive deliveries.
    /// </summary>
    Deactivated,
}
