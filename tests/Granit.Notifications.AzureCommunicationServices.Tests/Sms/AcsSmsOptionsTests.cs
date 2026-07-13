using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Tests;

public sealed class AcsSmsOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        AcsSmsOptions.SectionName.ShouldBe("Notifications:AzureCommunicationServices:Sms");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new AcsSmsOptions().TimeoutSeconds.ShouldBeGreaterThan(0);
}
