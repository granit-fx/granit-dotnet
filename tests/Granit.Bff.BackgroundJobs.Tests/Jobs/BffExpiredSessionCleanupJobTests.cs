using Granit.BackgroundJobs;
using Granit.Bff.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Bff.BackgroundJobs.Tests.Jobs;

public sealed class BffExpiredSessionCleanupJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_15_minute_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(BffExpiredSessionCleanupJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("*/15 * * * *");
        attr.Name.ShouldBe("bff-expired-session-cleanup");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(BffExpiredSessionCleanupJob)).ShouldBeTrue();
}
