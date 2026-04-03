using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Subscriptions.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.BackgroundJobs.Tests;

public sealed class SubscriptionsBackgroundJobTests
{
    // -------------------------------------------------------------------------
    // TrialExpirationScanJob
    // -------------------------------------------------------------------------

    [Fact]
    public void TrialExpirationScanJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(TrialExpirationScanJob)).ShouldBeTrue();
    }

    [Fact]
    public void TrialExpirationScanJob_ShouldBeSealedRecord()
    {
        typeof(TrialExpirationScanJob).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void TrialExpirationScanJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(TrialExpirationScanJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("subscriptions-trial-expiration-scan");
        attr.CronExpression.ShouldBe("0 */4 * * *");
    }

    // -------------------------------------------------------------------------
    // PeriodEndScanJob
    // -------------------------------------------------------------------------

    [Fact]
    public void PeriodEndScanJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(PeriodEndScanJob)).ShouldBeTrue();
    }

    [Fact]
    public void PeriodEndScanJob_ShouldBeSealedRecord()
    {
        typeof(PeriodEndScanJob).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void PeriodEndScanJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(PeriodEndScanJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("subscriptions-period-end-scan");
        attr.CronExpression.ShouldBe("0 * * * *");
    }

    // -------------------------------------------------------------------------
    // CancelAtPeriodEndScanJob
    // -------------------------------------------------------------------------

    [Fact]
    public void CancelAtPeriodEndScanJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(CancelAtPeriodEndScanJob)).ShouldBeTrue();
    }

    [Fact]
    public void CancelAtPeriodEndScanJob_ShouldBeSealedRecord()
    {
        typeof(CancelAtPeriodEndScanJob).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void CancelAtPeriodEndScanJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(CancelAtPeriodEndScanJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("subscriptions-cancel-at-period-end-scan");
        attr.CronExpression.ShouldBe("0 */2 * * *");
    }
}
