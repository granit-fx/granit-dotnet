using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="TrialExpirationScanJob"/>. Detects expiring and expired trials.
/// </summary>
internal static partial class TrialExpirationScanHandler
{
    public static async Task HandleAsync(
        TrialExpirationScanJob _,
        ISubscriptionReader reader,
        ISubscriptionWriter writer,
        IMessageBus messageBus,
        IClock clock,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<TrialExpirationScanJob> logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;
        DateTimeOffset warningThreshold = now.AddDays(3);

        IReadOnlyList<Subscription> expiringTrials;
        using (dataFilter.Disable<IMultiTenant>())
        {
            expiringTrials = await reader
                .GetExpiringTrialsAsync(warningThreshold, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (Subscription sub in expiringTrials)
        {
            try
            {
                using (currentTenant.Change(sub.TenantId))
                {
                    if (sub.TrialEndsAt <= now)
                    {
                        if (sub.Expire())
                        {
                            await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
                            Log.TrialExpired(logger, sub.Id);
                        }
                    }
                    else
                    {
                        int daysRemaining = (int)(sub.TrialEndsAt!.Value - now).TotalDays;
                        await messageBus.PublishAsync(
                            new TrialExpiringEvent(sub.Id, sub.PlanId, daysRemaining)).ConfigureAwait(false);
                        Log.TrialExpiring(logger, sub.Id, daysRemaining);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.TrialScanItemFailed(logger, sub.Id, ex);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Trial expired for subscription {SubscriptionId}")]
        public static partial void TrialExpired(ILogger logger, Guid subscriptionId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Trial expiring in {DaysRemaining} day(s) for subscription {SubscriptionId}")]
        public static partial void TrialExpiring(ILogger logger, Guid subscriptionId, int daysRemaining);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process trial scan for subscription {SubscriptionId}")]
        public static partial void TrialScanItemFailed(ILogger logger, Guid subscriptionId, Exception exception);
    }
}
