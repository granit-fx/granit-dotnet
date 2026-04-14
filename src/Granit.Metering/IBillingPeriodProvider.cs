namespace Granit.Metering;

/// <summary>
/// Provides billing period boundaries for quota enforcement.
/// </summary>
/// <remarks>
/// The default implementation uses the current calendar month. The Subscriptions
/// bridge overrides this with the tenant's actual subscription period so that
/// quota resets align with the billing cycle rather than calendar boundaries.
/// </remarks>
public interface IBillingPeriodProvider
{
    /// <summary>
    /// Returns the current billing period start and end for the given tenant,
    /// or <c>null</c> if no active subscription exists.
    /// </summary>
    Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>Billing period start and end boundaries used for quota calculations.</summary>
public sealed record BillingPeriodBoundaries(DateTimeOffset Start, DateTimeOffset End);
