namespace Granit.Subscriptions;

/// <summary>
/// Syncs subscription state changes to the external provider (Stripe, etc.).
/// </summary>
public interface ISubscriptionProviderSyncService
{
    /// <summary>
    /// Cancels the subscription in the external provider after an internal cancellation.
    /// </summary>
    /// <param name="subscriptionId">Subscription to cancel in the external provider.</param>
    /// <param name="tenantId">Owning tenant identifier.</param>
    /// <param name="cancellationToken"></param>
    Task SyncCancellationAsync(
        Guid subscriptionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
