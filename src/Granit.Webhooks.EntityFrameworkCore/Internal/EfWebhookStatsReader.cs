using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookStatsReader"/>. Aggregates dashboard
/// statistics across whatever scope the current request belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared.</b> Single-context aggregation, every row visible to the active scope.
/// </para>
/// <para>
/// <b>Segregated + tenant scope.</b> Sums host stats and active-tenant stats — a tenant
/// dashboard sees its own subscriptions plus host SIEM forwards relevant to its events.
/// </para>
/// <para>
/// <b>Segregated + host-admin scope</b> (Phase 2C). Iterates every tenant returned by
/// the framework-primitive <see cref="ITenantEnumerator"/> via
/// <see cref="ICurrentTenant.Change"/>, opens that tenant's isolated context, and sums
/// into the host aggregate. <b>O(N) connections per stats call, N = number of tenants</b>
/// — acceptable for the admin dashboard which is read rarely; a Postgres cross-schema
/// materialised view is the documented optimisation path for deployments with hundreds
/// of tenants. Soft-dep on <see cref="ITenantEnumerator"/> means this module never
/// references the <c>Granit.MultiTenancy</c> package directly — the default
/// <c>NullTenantEnumerator</c> returns an empty list, gracefully degrading to host-only
/// aggregation when no multi-tenancy stack is loaded.
/// </para>
/// </remarks>
internal sealed class EfWebhookStatsReader(
    WebhooksContextResolver resolver,
    ICurrentTenant currentTenant,
    ITenantEnumerator tenantEnumerator,
    IClock clock) : IWebhookStatsReader
{
    public async Task<WebhookStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = clock.Now.AddHours(-24);
        StatsAccumulator acc = new();

        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            await AggregateAcrossAllTenantsAsync(acc, cutoff, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            IReadOnlyList<IWebhooksDbContext> contexts = await resolver
                .OpenAllAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (IWebhooksDbContext db in contexts)
                {
                    await AggregateAsync(db, cutoff, acc, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await DisposeAllAsync(contexts).ConfigureAwait(false);
            }
        }

        return acc.Build();
    }

    private async Task AggregateAcrossAllTenantsAsync(
        StatsAccumulator acc,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        // Host context: scope already host-admin, open directly.
        await using (IWebhooksDbContext host = await resolver
            .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false))
        {
            await AggregateAsync(host, cutoff, acc, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<(Guid Id, string Name)> tenants = await tenantEnumerator
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Empty under NullTenantEnumerator (no Granit.MultiTenancy package loaded) —
        // the foreach short-circuits and host counts stand alone, no exception thrown.
        foreach ((Guid id, string name) in tenants)
        {
            using (currentTenant.Change(id, name))
            {
                await using IWebhooksDbContext db = await resolver
                    .OpenForScopeAsync(id, cancellationToken).ConfigureAwait(false);
                await AggregateAsync(db, cutoff, acc, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task AggregateAsync(
        IWebhooksDbContext db,
        DateTimeOffset cutoff,
        StatsAccumulator acc,
        CancellationToken cancellationToken)
    {
        List<WebhookSubscriptionStatus> statuses = await db.WebhookSubscriptions
            .AsNoTracking()
            .Select(s => s.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        acc.Subscriptions += statuses.Count;
        acc.Active += statuses.Count(s => s == WebhookSubscriptionStatus.Active);
        acc.Suspended += statuses.Count(s => s == WebhookSubscriptionStatus.Suspended);
        acc.Deactivated += statuses.Count(s => s == WebhookSubscriptionStatus.Deactivated);

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
            acc.Deliveries += deliveryStats.Total;
            acc.SuccessCount += deliveryStats.SuccessCount;
            acc.DurationSum += deliveryStats.DurationSum;
            acc.DurationSamples += deliveryStats.DurationSamples;
        }
    }

    private static async ValueTask DisposeAllAsync(IReadOnlyList<IWebhooksDbContext> contexts)
    {
        foreach (IWebhooksDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class StatsAccumulator
    {
        public int Subscriptions { get; set; }
        public int Active { get; set; }
        public int Suspended { get; set; }
        public int Deactivated { get; set; }
        public int Deliveries { get; set; }
        public int SuccessCount { get; set; }
        public double DurationSum { get; set; }
        public int DurationSamples { get; set; }

        public WebhookStats Build()
        {
            double successRate = Deliveries > 0
                ? Math.Round((double)SuccessCount / Deliveries * 100, 2)
                : 0.0;
            double avgResponse = DurationSamples > 0
                ? Math.Round(DurationSum / DurationSamples, 2)
                : 0.0;

            return new WebhookStats(
                Subscriptions,
                Active,
                Suspended,
                Deactivated,
                Deliveries,
                successRate,
                avgResponse);
        }
    }
}
