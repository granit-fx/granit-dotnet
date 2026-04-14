using Granit.Timing;

namespace Granit.Metering.Internal;

/// <summary>
/// Default <see cref="IBillingPeriodProvider"/> that returns the current calendar month.
/// </summary>
/// <remarks>
/// Used when no subscription-aware provider is registered. Always returns a period
/// so quota enforcement never fails silently due to a missing subscription.
/// </remarks>
internal sealed class CalendarMonthBillingPeriodProvider(IClock clock) : IBillingPeriodProvider
{
    public Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.Now;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        DateTimeOffset end = start.AddMonths(1);
        return Task.FromResult<BillingPeriodBoundaries?>(new BillingPeriodBoundaries(start, end));
    }
}
