using Granit.BackgroundJobs;
using Granit.DataExchange.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests.Jobs;

public sealed class DataExchangeRetentionSweepJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_daily_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(DataExchangeRetentionSweepJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 3 * * *");
        attr.Name.ShouldBe("data-exchange-retention-sweep");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(DataExchangeRetentionSweepJob)).ShouldBeTrue();
}
