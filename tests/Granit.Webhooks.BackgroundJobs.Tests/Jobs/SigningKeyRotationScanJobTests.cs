using Granit.BackgroundJobs;
using Granit.Webhooks.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.BackgroundJobs.Tests.Jobs;

public sealed class SigningKeyRotationScanJobTests
{
    [Fact]
    public void Should_have_RecurringJob_attribute_with_daily_cron()
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(
            typeof(SigningKeyRotationScanJob), typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe("0 7 * * *");
        attr.Name.ShouldBe("webhooks-key-rotation-scan");
    }

    [Fact]
    public void Should_implement_IBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(SigningKeyRotationScanJob)).ShouldBeTrue();
}
