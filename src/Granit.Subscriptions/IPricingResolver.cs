using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>
/// Resolves unit pricing for invoice line items based on plan configuration.
/// </summary>
public interface IPricingResolver
{
    /// <summary>Resolves the base plan price for a subscription billing cycle.</summary>
    Task<decimal> ResolveBasePriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves the per-unit price for usage-based billing.</summary>
    Task<decimal> ResolveUsageUnitPriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, CancellationToken cancellationToken = default);
}
