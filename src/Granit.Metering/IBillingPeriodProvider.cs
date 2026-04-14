namespace Granit.Metering;

/// <summary>
/// Provides the current billing period boundaries for a tenant.
/// </summary>
public interface IBillingPeriodProvider
{
    /// <summary>
    /// Returns the start and end of the current billing period for the given tenant,
    /// or <c>null</c> if no billing period is defined.
    /// </summary>
    Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the boundaries of a billing period.
/// </summary>
public sealed record BillingPeriodBoundaries(DateTimeOffset Start, DateTimeOffset End);
