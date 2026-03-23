using Granit.BackgroundJobs;
using Granit.OpenIddict.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictIdleSessionEnforcementJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_5_minute_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(OpenIddictIdleSessionEnforcementJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("*/5 * * * *");
        attr.Name.ShouldBe("openiddict-idle-session-enforcement");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(OpenIddictIdleSessionEnforcementJob)).ShouldBeTrue();
}
