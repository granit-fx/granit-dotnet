using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// Resolves pricing from <see cref="PlanPrice"/> entries via <see cref="IPlanReader"/>.
/// Supports pinned price resolution for grandfathered subscriptions.
/// </summary>
internal sealed class EfPricingResolver(IPlanReader planReader) : IPricingResolver
{
    public async Task<decimal> ResolveBasePriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        Guid? planPriceId = null,
        CancellationToken cancellationToken = default)
    {
        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            return 0m;
        }

        PlanPrice? price = ResolvePlanPrice(plan, currency, interval, planPriceId);
        return price?.Amount ?? 0m;
    }

    public async Task<decimal> ResolveUsageUnitPriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, Guid? planPriceId = null,
        CancellationToken cancellationToken = default)
    {
        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null || plan.PricingModel is not (PricingModel.PerUnit or PricingModel.Tiered))
        {
            return 0m;
        }

        PlanPrice? price = ResolvePlanPrice(plan, currency, interval, planPriceId);
        return price?.Amount ?? 0m;
    }

    private static PlanPrice? ResolvePlanPrice(
        Plan plan, string currency, BillingInterval interval, Guid? planPriceId)
    {
        // Pinned price: resolve by ID directly (grandfathered subscription)
        if (planPriceId.HasValue)
        {
            return plan.Prices.FirstOrDefault(p => p.Id == planPriceId.Value);
        }

        // Dynamic resolution: return the current active price for (currency, interval)
        return plan.GetCurrentPrice(currency, interval);
    }
}
