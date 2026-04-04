using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

internal sealed partial class DefaultCancelAtPeriodEndService(
    ISubscriptionReader reader,
    ISubscriptionWriter writer,
    IClock clock,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter,
    ILogger<DefaultCancelAtPeriodEndService> logger) : ICancelAtPeriodEndService
{
    public async Task CancelDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;

        IReadOnlyList<Subscription> pendingCancels;
        using (dataFilter.Disable<IMultiTenant>())
        {
            pendingCancels = await reader
                .GetPendingCancelAtPeriodEndAsync(now, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (Subscription sub in pendingCancels)
        {
            try
            {
                using (currentTenant.Change(sub.TenantId))
                {
                    if (sub.Cancel("Scheduled cancel at period end", now))
                    {
                        await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
                        Log.CancelledAtPeriodEnd(logger, sub.Id);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.CancelAtPeriodEndFailed(logger, sub.Id, ex);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Subscription {SubscriptionId} cancelled at period end")]
        public static partial void CancelledAtPeriodEnd(ILogger logger, Guid subscriptionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to cancel subscription {SubscriptionId} at period end")]
        public static partial void CancelAtPeriodEndFailed(ILogger logger, Guid subscriptionId, Exception exception);
    }
}
