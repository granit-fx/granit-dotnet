using Granit.Metering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Internal;

/// <summary>
/// <see cref="IBillingPeriodProvider"/> implementation that aligns quota enforcement
/// with the tenant's actual subscription billing period.
/// </summary>
/// <remarks>
/// Registered by <c>Granit.Subscriptions</c> to override the default calendar-month
/// fallback from <c>Granit.Metering</c>. When no active subscription exists the method
/// returns <c>null</c>, causing <see cref="Granit.Metering.EntityFrameworkCore"/> to
/// fall back to the current calendar month.
/// </remarks>
internal sealed class SubscriptionBillingPeriodProvider(
    ISubscriptionReader subscriptionReader) : IBillingPeriodProvider
{
    public async Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return null;
        }

        return new BillingPeriodBoundaries(
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd);
    }
}
