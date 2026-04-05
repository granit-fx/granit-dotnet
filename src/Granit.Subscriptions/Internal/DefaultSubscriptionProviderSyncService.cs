using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

internal sealed partial class DefaultSubscriptionProviderSyncService(
    ISubscriptionReader subscriptionReader,
    ISubscriptionProvider provider,
    ILogger<DefaultSubscriptionProviderSyncService> logger) : ISubscriptionProviderSyncService
{
    public async Task SyncCancellationAsync(
        Guid subscriptionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await subscriptionReader
            .GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            Log.SubscriptionNotFound(logger, subscriptionId);
            return;
        }

        if (subscription.TenantId != tenantId)
        {
            Log.TenantMismatch(logger, subscriptionId, tenantId, subscription.TenantId);
            return;
        }

        SubscriptionExternalMapping? mapping = subscription.ExternalMappings
            .FirstOrDefault(m => m.ProviderName == provider.Name);

        if (mapping is not null)
        {
            await provider.CancelExternalAsync(mapping, atPeriodEnd: false, cancellationToken)
                .ConfigureAwait(false);
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
