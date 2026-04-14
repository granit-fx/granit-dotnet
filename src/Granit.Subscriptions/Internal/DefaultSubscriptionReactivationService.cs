using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

internal sealed partial class DefaultSubscriptionReactivationService(
    ISubscriptionReader subscriptionReader,
    ISubscriptionWriter subscriptionWriter,
    ILogger<DefaultSubscriptionReactivationService> logger) : ISubscriptionReactivationService
{
    public async Task<bool> TryReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await subscriptionReader
            .GetPastDueForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return false;
        }

        subscription.Activate();
        subscription.ResetDunning();
        await subscriptionWriter.UpdateAsync(subscription, cancellationToken)
            .ConfigureAwait(false);
        Log.Reactivated(logger, subscription.Id);
        return true;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Subscription {SubscriptionId} reactivated after invoice payment")]
        public static partial void Reactivated(ILogger logger, Guid subscriptionId);
    }
}
