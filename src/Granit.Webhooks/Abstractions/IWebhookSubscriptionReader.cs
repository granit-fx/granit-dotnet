using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Read operations for webhook subscriptions.
/// </summary>
public interface IWebhookSubscriptionReader
{
    /// <summary>
    /// Returns all active subscriptions matching the given event type and tenant context.
    /// </summary>
    /// <remarks>
    /// Subscriptions with <c>TenantId = null</c> (global) are always included regardless
    /// of the <paramref name="tenantId"/> value.
    /// </remarks>
    Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the subscription with the given identifier, or <c>null</c> if not found.</summary>
    Task<WebhookSubscription?> FindByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all subscriptions regardless of status.</summary>
    Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default);
}
