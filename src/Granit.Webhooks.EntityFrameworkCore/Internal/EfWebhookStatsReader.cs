using Granit.Persistence.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookStatsReader"/>. Provides aggregate
/// statistics for the webhook administration dashboard, dispatching through
/// <see cref="WebhooksContextResolver"/> so the same reader serves both
/// <c>Shared</c> and <c>Segregated</c> storage modes.
/// </summary>
/// <remarks>
/// Under <see cref="DualScopeStorageMode.Segregated"/>, the reader aggregates across the
/// host context and the active tenant context — the cross-tenant host-admin view that
/// iterates every tenant schema is deferred to a follow-up PR (Phase 2C of #2377). Today
/// a host admin viewing stats under <c>Segregated</c> sees only host-scope counts.
/// </remarks>
internal sealed class EfWebhookStatsReader(
    WebhooksContextResolver resolver,
    IClock clock) : IWebhookStatsReader
{
    public async Task<WebhookStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = clock.Now.AddHours(-24);

        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int totalSubscriptions = 0;
            int activeCount = 0;
            int suspendedCount = 0;
            int deactivatedCount = 0;
            int deliveriesLast24h = 0;
            int successCountLast24h = 0;
            double durationSumMs = 0;
            int durationSamples = 0;

            foreach (IWebhooksDbContext db in contexts)
            {
                List<WebhookSubscriptionStatus> statuses = await db.WebhookSubscriptions
                    .AsNoTracking()
                    .Select(s => s.Status)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                totalSubscriptions += statuses.Count;
                activeCount += statuses.Count(s => s == WebhookSubscriptionStatus.Active);
                suspendedCount += statuses.Count(s => s == WebhookSubscriptionStatus.Suspended);
                deactivatedCount += statuses.Count(s => s == WebhookSubscriptionStatus.Deactivated);

                var deliveryStats = await db.WebhookDeliveryAttempts
                    .AsNoTracking()
                    .Where(d => d.OccurredAt >= cutoff)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        SuccessCount = g.Count(d => d.IsSuccess),
                        DurationSum = (double?)g
                            .Where(d => d.HttpStatusCode != null)
                            .Sum(d => (long?)d.DurationMs) ?? 0.0,
                        DurationSamples = g.Count(d => d.HttpStatusCode != null),
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (deliveryStats is not null)
                {
                    deliveriesLast24h += deliveryStats.Total;
                    successCountLast24h += deliveryStats.SuccessCount;
                    durationSumMs += deliveryStats.DurationSum;
                    durationSamples += deliveryStats.DurationSamples;
                }
            }

            double successRateLast24h = deliveriesLast24h > 0
                ? Math.Round((double)successCountLast24h / deliveriesLast24h * 100, 2)
                : 0.0;
            double avgResponseTimeMsLast24h = durationSamples > 0
                ? Math.Round(durationSumMs / durationSamples, 2)
                : 0.0;

            return new WebhookStats(
                totalSubscriptions,
                activeCount,
                suspendedCount,
                deactivatedCount,
                deliveriesLast24h,
                successRateLast24h,
                avgResponseTimeMsLast24h);
        }
        finally
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                await db.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
