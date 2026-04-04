namespace Granit.Subscriptions;

/// <summary>
/// Reactivates a PastDue subscription after successful payment.
/// Resets the dunning counter.
/// </summary>
public interface ISubscriptionReactivationService
{
    /// <summary>
    /// Reactivates the active subscription for the given tenant if it is in PastDue status.
    /// </summary>
    /// <param name="tenantId">Tenant whose subscription to reactivate.</param>
    /// <param name="cancellationToken"></param>
    /// <returns><c>true</c> if the subscription was reactivated; <c>false</c> if not in PastDue status.</returns>
    Task<bool> TryReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
