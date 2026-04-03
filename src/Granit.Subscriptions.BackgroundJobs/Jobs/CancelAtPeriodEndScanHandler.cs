using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="CancelAtPeriodEndScanJob"/>. Cancels subscriptions at period end.
/// </summary>
internal static partial class CancelAtPeriodEndScanHandler
{
    public static async Task HandleAsync(
        CancelAtPeriodEndScanJob _,
        ISubscriptionReader reader,
        ISubscriptionWriter writer,
        IClock clock,
        ICurrentTenant currentTenant,
        ILogger<CancelAtPeriodEndScanJob> logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;

        IReadOnlyList<Subscription> pendingCancels = await reader
            .GetPendingCancelAtPeriodEndAsync(now, cancellationToken)
            .ConfigureAwait(false);

        foreach (Subscription sub in pendingCancels)
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
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Subscription {SubscriptionId} cancelled at period end")]
        public static partial void CancelledAtPeriodEnd(ILogger logger, Guid subscriptionId);
    }
}
