using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// Resolves pricing from <see cref="PlanPrice"/> entries via <see cref="IPlanReader"/>.
/// </summary>
internal sealed class EfPricingResolver(IPlanReader planReader) : IPricingResolver
{
    public async Task<decimal> ResolveBasePriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        CancellationToken cancellationToken = default)
    {
        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            return 0m;
        }

        PlanPrice? price = plan.Prices
            .FirstOrDefault(p =>
                string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase)
                && p.Interval == interval);

        return price?.Amount ?? 0m;
    }

    public async Task<decimal> ResolveUsageUnitPriceAsync(
        PlanId planId, string currency, BillingInterval interval,
        string meterId, CancellationToken cancellationToken = default)
    {
        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null || plan.PricingModel is not (PricingModel.PerUnit or PricingModel.Tiered))
        {
            return 0m;
        }

        PlanPrice? price = plan.Prices
            .FirstOrDefault(p =>
                string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase)
                && p.Interval == interval);

        return price?.Amount ?? 0m;
    }
}
