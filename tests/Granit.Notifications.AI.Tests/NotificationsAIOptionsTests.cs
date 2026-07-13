using Granit.Notifications.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AI.Tests;

public sealed class NotificationsAIOptionsTests
{
    /// <summary>Pins the configuration section path so a rename surfaces in CI (checklist §1d).</summary>
    [Fact]
    public void SectionName_IsPinned() =>
        NotificationsAIOptions.SectionName.ShouldBe("Notifications:AI");

    [Fact]
    public void TimeoutSeconds_Default_IsPositive() =>
        new NotificationsAIOptions().TimeoutSeconds.ShouldBeGreaterThan(0);

    [Fact]
    public void AllowPersonalDataInPrompts_Default_IsFalse() =>
        new NotificationsAIOptions().AllowPersonalDataInPrompts.ShouldBeFalse();
}
