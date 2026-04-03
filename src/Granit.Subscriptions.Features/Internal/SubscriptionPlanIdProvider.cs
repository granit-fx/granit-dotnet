using Granit.Features.Plans;
using Granit.MultiTenancy;

namespace Granit.Subscriptions.Features.Internal;

/// <summary>
/// Resolves the current tenant's active subscription plan ID for the Features cascade.
/// </summary>
internal sealed class SubscriptionPlanIdProvider(
    ISubscriptionReader subscriptionReader,
    ICurrentTenant currentTenant) : IPlanIdProvider
{
    /// <inheritdoc/>
    public async Task<string?> GetCurrentPlanIdAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.IsAvailable || !currentTenant.Id.HasValue)
        {
            return null;
        }

        Domain.Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(currentTenant.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        return subscription?.PlanId.Value.ToString();
    }
}
