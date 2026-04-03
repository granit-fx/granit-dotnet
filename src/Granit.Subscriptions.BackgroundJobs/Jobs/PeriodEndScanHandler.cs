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
        IClock clock,
        ICurrentTenant currentTenant,
        ILogger<PeriodEndScanJob> logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;

        IReadOnlyList<Subscription> atPeriodEnd = await reader
            .GetAtPeriodEndAsync(now, cancellationToken)
            .ConfigureAwait(false);

        foreach (Subscription sub in atPeriodEnd)
        {
            using (currentTenant.Change(sub.TenantId))
            {
                DateTimeOffset newStart = sub.CurrentPeriodEnd;
                DateTimeOffset newEnd = CalculateNextPeriodEnd(newStart, sub);

                sub.AdvancePeriod(newStart, newEnd);
                await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
                Log.PeriodAdvanced(logger, sub.Id, newStart, newEnd);
            }
        }
    }

    private static DateTimeOffset CalculateNextPeriodEnd(
        DateTimeOffset periodStart, Subscription sub) =>
        periodStart.AddMonths(1); // Simplified — real implementation reads Plan.DefaultInterval

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Period advanced for subscription {SubscriptionId}: {PeriodStart} → {PeriodEnd}")]
        public static partial void PeriodAdvanced(ILogger logger, Guid subscriptionId, DateTimeOffset periodStart, DateTimeOffset periodEnd);
    }
}
