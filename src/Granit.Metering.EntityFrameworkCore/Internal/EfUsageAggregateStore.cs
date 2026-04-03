using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUsageReader"/>.
/// Reads pre-computed <see cref="UsageAggregate"/> rollups.
/// </summary>
internal sealed class EfUsageAggregateStore(
    IDbContextFactory<MeteringDbContext> contextFactory) : IUsageReader
{
    public async Task<UsageAggregate?> GetForPeriodAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        await using MeteringDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.UsageAggregates
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId
                    && a.MeterDefinitionId == meterId.Value
                    && a.PeriodStart == periodStart
                    && a.PeriodEnd == periodEnd,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UsageAggregate?> GetCurrentAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default)
    {
        await using MeteringDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.UsageAggregates
            .Where(a => a.TenantId == tenantId
                && a.MeterDefinitionId == meterId.Value
                && a.Period == AggregationPeriod.BillingPeriod)
            .OrderByDescending(a => a.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UsageAggregate>> GetAllForPeriodAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        await using MeteringDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.UsageAggregates
            .Where(a => a.TenantId == tenantId
                && a.PeriodStart >= periodStart
                && a.PeriodEnd <= periodEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
