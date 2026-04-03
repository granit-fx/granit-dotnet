using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="PeriodEndScanJob"/>. Advances billing periods for active subscriptions.
/// </summary>
internal static partial class PeriodEndScanHandler
{
    public static async Task HandleAsync(
        PeriodEndScanJob _,
        ISubscriptionReader reader,
        ISubscriptionWriter writer,
        IPlanReader planReader,
        IClock clock,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<PeriodEndScanJob> logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;

        IReadOnlyList<Subscription> atPeriodEnd;
        using (dataFilter.Disable<IMultiTenant>())
        {
            atPeriodEnd = await reader
                .GetAtPeriodEndAsync(now, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (Subscription sub in atPeriodEnd)
        {
            try
            {
                using (currentTenant.Change(sub.TenantId))
                {
                    Plan? plan = await planReader
                        .GetByIdAsync(sub.PlanId, cancellationToken)
                        .ConfigureAwait(false);

                    BillingInterval interval = plan?.DefaultInterval ?? BillingInterval.Monthly;

                    DateTimeOffset newStart = sub.CurrentPeriodEnd;
                    DateTimeOffset newEnd = AdvanceByInterval(newStart, interval);

                    sub.AdvancePeriod(newStart, newEnd);
                    await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
                    Log.PeriodAdvanced(logger, sub.Id, newStart, newEnd);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.PeriodAdvanceFailed(logger, sub.Id, ex);
            }
        }
    }

    internal static DateTimeOffset AdvanceByInterval(
        DateTimeOffset periodStart, BillingInterval interval) =>
        interval switch
        {
            BillingInterval.Monthly => periodStart.AddMonths(1),
            BillingInterval.Quarterly => periodStart.AddMonths(3),
            BillingInterval.Yearly => periodStart.AddMonths(12),
            _ => periodStart.AddMonths(1),
        };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Period advanced for subscription {SubscriptionId}: {PeriodStart} -> {PeriodEnd}")]
        public static partial void PeriodAdvanced(ILogger logger, Guid subscriptionId, DateTimeOffset periodStart, DateTimeOffset periodEnd);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to advance period for subscription {SubscriptionId}")]
        public static partial void PeriodAdvanceFailed(ILogger logger, Guid subscriptionId, Exception exception);
    }
}
