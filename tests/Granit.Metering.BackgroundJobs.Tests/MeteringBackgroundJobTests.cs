using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Metering.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Metering.BackgroundJobs.Tests;

public sealed class MeteringBackgroundJobTests
{
    // -------------------------------------------------------------------------
    // MeteringAggregationJob
    // -------------------------------------------------------------------------

    [Fact]
    public void MeteringAggregationJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(MeteringAggregationJob)).ShouldBeTrue();
    }

    [Fact]
    public void MeteringAggregationJob_ShouldBeSealedRecord()
    {
        typeof(MeteringAggregationJob).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void MeteringAggregationJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(MeteringAggregationJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("metering-aggregation");
        attr.CronExpression.ShouldBe("0 */1 * * *");
    }

    // -------------------------------------------------------------------------
    // QuotaThresholdCheckJob
    // -------------------------------------------------------------------------

    [Fact]
    public void QuotaThresholdCheckJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(QuotaThresholdCheckJob)).ShouldBeTrue();
    }

    [Fact]
    public void QuotaThresholdCheckJob_ShouldBeSealedRecord()
    {
        typeof(QuotaThresholdCheckJob).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void QuotaThresholdCheckJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(QuotaThresholdCheckJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("metering-quota-check");
        attr.CronExpression.ShouldBe("*/15 * * * *");
    }
}
