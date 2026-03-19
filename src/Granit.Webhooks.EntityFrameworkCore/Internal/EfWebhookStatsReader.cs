using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookStatsReader"/>.
/// Provides aggregate statistics for the webhook administration dashboard.
/// </summary>
internal sealed class EfWebhookStatsReader(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    IClock clock) : IWebhookStatsReader
{
    public async Task<WebhookStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset cutoff = clock.Now.AddHours(-24);

        // Subscription counts by status
        List<WebhookSubscriptionStatus> allStatuses = await context.WebhookSubscriptions
            .AsNoTracking()
            .Select(s => s.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int totalSubscriptions = allStatuses.Count;
        int activeCount = allStatuses.Count(s => s == WebhookSubscriptionStatus.Active);
        int suspendedCount = allStatuses.Count(s => s == WebhookSubscriptionStatus.Suspended);
        int deactivatedCount = allStatuses.Count(s => s == WebhookSubscriptionStatus.Deactivated);

        // Delivery stats for last 24 hours
        var deliveryStats = await context.WebhookDeliveryAttempts
            .AsNoTracking()
            .Where(d => d.OccurredAt >= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                SuccessCount = g.Count(d => d.IsSuccess),
                AvgResponseTimeMs = g
                    .Where(d => d.HttpStatusCode != null)
                    .Average(d => (double?)d.DurationMs) ?? 0.0,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int deliveriesLast24h = deliveryStats?.Total ?? 0;
        double successRateLast24h = deliveriesLast24h > 0
            ? Math.Round((double)deliveryStats!.SuccessCount / deliveriesLast24h * 100, 2)
            : 0.0;
        double avgResponseTimeMsLast24h = Math.Round(deliveryStats?.AvgResponseTimeMs ?? 0.0, 2);

        return new WebhookStats(
            totalSubscriptions,
            activeCount,
            suspendedCount,
            deactivatedCount,
            deliveriesLast24h,
            successRateLast24h,
            avgResponseTimeMsLast24h);
    }
}
