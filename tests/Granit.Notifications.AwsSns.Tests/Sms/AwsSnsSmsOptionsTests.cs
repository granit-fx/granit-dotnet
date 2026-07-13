using Granit.Notifications.AwsSns.Sms.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Sms.Tests;

public sealed class AwsSnsSmsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        AwsSnsSmsOptions.SectionName.ShouldBe("Notifications:AwsSns:Sms");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new AwsSnsSmsOptions().TimeoutSeconds.ShouldBeGreaterThan(0);
}
