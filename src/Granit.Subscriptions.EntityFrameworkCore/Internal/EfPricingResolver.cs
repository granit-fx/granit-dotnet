using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Pricing;

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

    public async Task<decimal> ResolveUsageAmountAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, decimal quantity, Guid? planPriceId = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0m)
        {
            return 0m;
        }

        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null || plan.PricingModel is not (PricingModel.PerUnit or PricingModel.Tiered))
        {
            return 0m;
        }

        PlanPrice? price = ResolvePlanPrice(plan, currency, interval, planPriceId);
        if (price is null)
        {
            return 0m;
        }

        // Tiered + non-empty tier list → bracket math; otherwise fall back to flat unit price.
        if (price.TieringMode is { } mode && price.Tiers.Count > 0)
        {
            return TieredPricingCalculator.Compute(quantity, mode, price.Tiers);
        }

        return quantity * price.Amount;
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
