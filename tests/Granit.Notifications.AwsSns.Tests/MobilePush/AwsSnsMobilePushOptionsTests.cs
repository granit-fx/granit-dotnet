using Granit.Notifications.AwsSns.MobilePush.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.MobilePush.Tests;

public sealed class AwsSnsMobilePushOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        AwsSnsMobilePushOptions.SectionName.ShouldBe("Notifications:AwsSns:MobilePush");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new AwsSnsMobilePushOptions().TimeoutSeconds.ShouldBeGreaterThan(0);
}
