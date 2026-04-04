using Granit.Metering.Dtos;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="QuotaStatus"/> for API responses.
/// </summary>
/// <param name="MeterName">Display name of the meter.</param>
/// <param name="CurrentUsage">Current usage value in the active billing period.</param>
/// <param name="Limit">The plan-defined limit (null if unlimited).</param>
/// <param name="PercentUsed">Usage as a percentage of the limit (null if unlimited).</param>
/// <param name="IsExceeded">Whether usage has reached or exceeded the limit.</param>
public sealed record MeteringQuotaStatusResponse(
    string MeterName,
    decimal CurrentUsage,
    decimal? Limit,
    decimal? PercentUsed,
    bool IsExceeded)
{
    internal static MeteringQuotaStatusResponse FromQuotaStatus(QuotaStatus status) => new(
        status.MeterName,
        status.CurrentUsage,
        status.Limit,
        status.PercentUsed,
        status.IsExceeded);
}
