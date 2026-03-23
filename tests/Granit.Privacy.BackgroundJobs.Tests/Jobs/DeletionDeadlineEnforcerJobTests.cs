using Granit.BackgroundJobs;
using Granit.Privacy.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BackgroundJobs.Tests.Jobs;

public sealed class DeletionDeadlineEnforcerJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_daily_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(DeletionDeadlineEnforcerJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 2 * * *");
        attr.Name.ShouldBe("privacy-deletion-deadline-enforcer");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(DeletionDeadlineEnforcerJob)).ShouldBeTrue();
}
