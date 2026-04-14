using Granit.Timing;

namespace Granit.Metering.Internal;

internal sealed class CalendarMonthBillingPeriodProvider(IClock clock) : IBillingPeriodProvider
{
    public Task<BillingPeriodBoundaries?> GetCurrentPeriodAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.Now;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        DateTimeOffset end = start.AddMonths(1);
        return Task.FromResult<BillingPeriodBoundaries?>(new BillingPeriodBoundaries(start, end));
    }
}
