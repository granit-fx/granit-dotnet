using Granit.BackgroundJobs;
using Granit.OpenIddict.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictKeyRotationJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_daily_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(OpenIddictKeyRotationJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 3 * * *");
        attr.Name.ShouldBe("openiddict-key-rotation");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(OpenIddictKeyRotationJob)).ShouldBeTrue();
}
