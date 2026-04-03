using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;

namespace Granit.Metering;

/// <summary>Reads pre-computed usage aggregates.</summary>
public interface IUsageReader
{
    /// <summary>Returns aggregated usage for a meter over a specific period.</summary>
    Task<UsageAggregate?> GetForPeriodAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the current billing-period aggregate for a meter.</summary>
    Task<UsageAggregate?> GetCurrentAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all aggregates for a tenant in a given period.</summary>
    Task<IReadOnlyList<UsageAggregate>> GetAllForPeriodAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}
