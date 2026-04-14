using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQuotaChecker"/>.
/// Reads current billing-period usage from hourly aggregates, scoped to the
/// tenant's actual billing period via <see cref="IBillingPeriodProvider"/>.
/// </summary>
internal sealed class EfQuotaChecker(
    IDbContextFactory<MeteringDbContext> contextFactory,
    IMeterDefinitionReader definitionReader,
    IQuotaLimitProvider quotaLimitProvider,
    IBillingPeriodProvider billingPeriodProvider,
    IClock clock) : IQuotaChecker
{
    public async Task<QuotaStatus> CheckAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default)
    {
        MeterDefinition? definition = await definitionReader
            .GetByIdAsync(meterId, cancellationToken).ConfigureAwait(false);

        if (definition is null)
        {
            return QuotaStatus.Unlimited("unknown", 0);
        }

        BillingPeriodBoundaries? period = await billingPeriodProvider
            .GetCurrentPeriodAsync(tenantId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        DateTimeOffset start = period?.Start ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        DateTimeOffset end = period?.End ?? start.AddMonths(1);

        await using MeteringDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        decimal currentUsage = await db.UsageAggregates
            .Where(a => a.TenantId == tenantId
                && a.MeterDefinitionId == meterId.Value
                && a.Period == AggregationPeriod.Hourly
                && a.PeriodStart >= start
                && a.PeriodEnd <= end)
            .SumAsync(a => a.AggregatedValue, cancellationToken)
            .ConfigureAwait(false);

        decimal? limit = await quotaLimitProvider
            .GetLimitAsync(tenantId, meterId, cancellationToken).ConfigureAwait(false);

        return limit.HasValue
            ? QuotaStatus.WithLimit(definition.Name, currentUsage, limit.Value)
            : QuotaStatus.Unlimited(definition.Name, currentUsage);
    }
}
