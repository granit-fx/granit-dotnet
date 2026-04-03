using Granit.Metering.Domain.ValueObjects;

namespace Granit.Metering;

/// <summary>
/// Provides the plan-defined quota limit for a given tenant and meter.
/// </summary>
/// <remarks>
/// The default implementation returns <c>null</c> (unlimited). Override with a
/// concrete provider (e.g., from the Subscriptions module) to enforce real quotas.
/// </remarks>
public interface IQuotaLimitProvider
{
    /// <summary>Returns the quota limit, or <c>null</c> if the meter is unlimited.</summary>
    Task<decimal?> GetLimitAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IQuotaLimitProvider"/> that treats all meters as unlimited.
/// </summary>
internal sealed class UnlimitedQuotaLimitProvider : IQuotaLimitProvider
{
    public Task<decimal?> GetLimitAsync(
        Guid tenantId,
        MeterDefinitionId meterId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);
}
