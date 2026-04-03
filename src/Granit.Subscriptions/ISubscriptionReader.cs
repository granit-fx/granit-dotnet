using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>Reads subscription data (query side of CQRS).</summary>
public interface ISubscriptionReader
{
    /// <summary>Returns a subscription by ID.</summary>
    Task<Subscription?> GetByIdAsync(SubscriptionId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the active subscription for a tenant (Active or Trial).</summary>
    Task<Subscription?> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns all subscriptions for a tenant.</summary>
    Task<IReadOnlyList<Subscription>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns subscriptions with trials expiring before the threshold.</summary>
    Task<IReadOnlyList<Subscription>> GetExpiringTrialsAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken = default);

    /// <summary>Returns subscriptions reaching the end of their current billing period.</summary>
    Task<IReadOnlyList<Subscription>> GetAtPeriodEndAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken = default);

    /// <summary>Returns subscriptions flagged for cancel at period end that have passed their period end.</summary>
    Task<IReadOnlyList<Subscription>> GetPendingCancelAtPeriodEndAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
