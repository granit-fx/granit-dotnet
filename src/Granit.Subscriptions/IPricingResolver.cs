using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>
/// Resolves unit pricing for invoice line items based on plan configuration.
/// Supports pinned price resolution for grandfathered subscriptions.
/// </summary>
public interface IPricingResolver
{
    /// <summary>
    /// Resolves the base plan price for a subscription billing cycle.
    /// When <paramref name="planPriceId"/> is provided, returns the pinned price amount directly.
    /// Otherwise, resolves the current active price from the plan's price catalog.
    /// </summary>
    Task<decimal> ResolveBasePriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        Guid? planPriceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the per-unit price for usage-based billing.
    /// When <paramref name="planPriceId"/> is provided, returns the pinned price amount directly.
    /// Otherwise, resolves the current active price from the plan's price catalog.
    /// </summary>
    Task<decimal> ResolveUsageUnitPriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, Guid? planPriceId = null,
        CancellationToken cancellationToken = default);
}
