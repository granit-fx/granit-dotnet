using Granit.Notifications.AwsSes.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

public sealed class AwsSesOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        AwsSesOptions.SectionName.ShouldBe("Notifications:AwsSes");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new AwsSesOptions().TimeoutSeconds.ShouldBeGreaterThan(0);
}
