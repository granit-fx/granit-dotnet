using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Reactivates a PastDue subscription when its invoice is paid.
/// Resets the dunning counter to allow future billing cycles.
/// </summary>
internal static partial class InvoicePaidHandler
{
    public static async Task HandleAsync(
        InvoicePaidEto eto,
        ISubscriptionReader subscriptionReader,
        ISubscriptionWriter subscriptionWriter,
        ICurrentTenant currentTenant,
        ILogger<InvoicePaidEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            Subscription? subscription = await subscriptionReader
                .GetActiveForTenantAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                return;
            }

            if (subscription.Status == SubscriptionStatus.PastDue)
            {
                subscription.Activate();
                subscription.ResetDunning();
                await subscriptionWriter.UpdateAsync(subscription, cancellationToken)
                    .ConfigureAwait(false);
                Log.Reactivated(logger, subscription.Id);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Subscription {SubscriptionId} reactivated after invoice payment")]
        public static partial void Reactivated(ILogger logger, Guid subscriptionId);
    }
}
