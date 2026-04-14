using Granit.Metering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Internal;

internal sealed class SubscriptionBillingPeriodProvider(
    ISubscriptionReader subscriptionReader) : IBillingPeriodProvider
{
    public async Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        return subscription is null
            ? null
            : new BillingPeriodBoundaries(subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd);
    }
}
