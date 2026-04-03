namespace Granit.Metering.Dtos;

/// <summary>
/// Represents the current quota status for a meter.
/// </summary>
/// <param name="MeterName">Display name of the meter.</param>
/// <param name="CurrentUsage">Current usage value in the active billing period.</param>
/// <param name="Limit">The plan-defined limit (null if unlimited).</param>
/// <param name="PercentUsed">Usage as a percentage of the limit (null if unlimited).</param>
/// <param name="IsExceeded">Whether usage has reached or exceeded the limit.</param>
public sealed record QuotaStatus(
    string MeterName,
    decimal CurrentUsage,
    decimal? Limit,
    decimal? PercentUsed,
    bool IsExceeded)
{
    /// <summary>Creates a quota status for an unlimited meter.</summary>
    public static QuotaStatus Unlimited(string meterName, decimal currentUsage) =>
        new(meterName, currentUsage, Limit: null, PercentUsed: null, IsExceeded: false);

    /// <summary>Creates a quota status with a defined limit.</summary>
    public static QuotaStatus WithLimit(string meterName, decimal currentUsage, decimal limit) =>
        new(
            meterName,
            currentUsage,
            limit,
            PercentUsed: limit > 0 ? Math.Round(currentUsage / limit * 100, 2) : 100m,
            IsExceeded: currentUsage >= limit);
}
