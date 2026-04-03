using Granit.MultiTenancy;
using Granit.Subscriptions.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Syncs subscription state to the external provider after FSM transitions.
/// </summary>
internal static partial class SubscriptionProviderSyncHandler
{
    /// <summary>
    /// After a subscription is cancelled in Granit, cancel it in the external provider.
    /// </summary>
    public static async Task HandleAsync(
        SubscriptionCancelledEto eto,
        ISubscriptionReader subscriptionReader,
        ISubscriptionProvider provider,
        ICurrentTenant currentTenant,
        ILogger<SubscriptionCancelledEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            Domain.Subscription? subscription = await subscriptionReader
                .GetByIdAsync(eto.SubscriptionId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                Log.SubscriptionNotFound(logger, eto.SubscriptionId);
                return;
            }

            if (subscription.TenantId != eto.TenantId)
            {
                Log.TenantMismatch(logger, eto.SubscriptionId, eto.TenantId, subscription.TenantId);
                return;
            }

            Domain.SubscriptionExternalMapping? mapping = subscription.ExternalMappings
                .FirstOrDefault(m => m.ProviderName == provider.Name);

            if (mapping is not null)
            {
                await provider.CancelExternalAsync(mapping, atPeriodEnd: false, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Provider sync: subscription {SubscriptionId} not found")]
        public static partial void SubscriptionNotFound(ILogger logger, Guid subscriptionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Provider sync: tenant mismatch for subscription {SubscriptionId} — ETO tenant {EtoTenantId}, actual tenant {ActualTenantId}")]
        public static partial void TenantMismatch(ILogger logger, Guid subscriptionId, Guid etoTenantId, Guid? actualTenantId);
    }
}
