using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Email.Tests;

public sealed class AcsEmailOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        AcsEmailOptions.SectionName.ShouldBe("Notifications:AzureCommunicationServices:Email");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new AcsEmailOptions().TimeoutSeconds.ShouldBeGreaterThan(0);
}
