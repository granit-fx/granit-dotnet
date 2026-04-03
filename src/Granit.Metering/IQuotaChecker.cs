using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;

namespace Granit.Metering;

/// <summary>Checks current usage against plan-defined quotas.</summary>
public interface IQuotaChecker
{
    /// <summary>Returns the quota status for a specific meter and tenant.</summary>
    Task<QuotaStatus> CheckAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default);
}
