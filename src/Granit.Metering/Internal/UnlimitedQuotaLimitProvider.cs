using Granit.Metering.Domain.ValueObjects;

namespace Granit.Metering.Internal;

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
