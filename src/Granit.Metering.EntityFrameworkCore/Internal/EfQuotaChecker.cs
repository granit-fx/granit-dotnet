using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQuotaChecker"/>.
/// Reads current billing-period usage from aggregates and quota limits
/// from <see cref="IQuotaLimitProvider"/>.
/// </summary>
internal sealed class EfQuotaChecker(
    IDbContextFactory<MeteringDbContext> contextFactory,
    IMeterDefinitionReader definitionReader,
    IQuotaLimitProvider quotaLimitProvider) : IQuotaChecker
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

        await using MeteringDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        UsageAggregate? currentAggregate = await db.UsageAggregates
            .Where(a => a.TenantId == tenantId
                && a.MeterDefinitionId == meterId.Value
                && a.Period == AggregationPeriod.BillingPeriod)
            .OrderByDescending(a => a.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal currentUsage = currentAggregate?.AggregatedValue ?? 0;

        decimal? limit = await quotaLimitProvider
            .GetLimitAsync(tenantId, meterId, cancellationToken).ConfigureAwait(false);

        return limit.HasValue
            ? QuotaStatus.WithLimit(definition.Name, currentUsage, limit.Value)
            : QuotaStatus.Unlimited(definition.Name, currentUsage);
    }
}
