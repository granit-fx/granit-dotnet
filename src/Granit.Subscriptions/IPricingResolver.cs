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

    /// <summary>
    /// Resolves the total amount owed for <paramref name="quantity"/> units of usage,
    /// applying the plan's <see cref="PlanPrice.TieringMode"/> and tier sequence when
    /// the price is tiered. For non-tiered prices, falls back to <c>quantity × unit price</c>.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> when the plan is not found, the price cannot be resolved, the
    /// plan's <c>PricingModel</c> is not a usage variant (PerUnit / Tiered), or the
    /// quantity is zero.
    /// </remarks>
    Task<decimal> ResolveUsageAmountAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, decimal quantity, Guid? planPriceId = null,
        CancellationToken cancellationToken = default);
}
