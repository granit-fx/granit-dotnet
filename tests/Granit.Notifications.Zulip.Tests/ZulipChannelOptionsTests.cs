using Granit.Notifications.Zulip.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Zulip.Tests;

public sealed class ZulipChannelOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        ZulipChannelOptions.SectionName.ShouldBe("Notifications:Zulip");

    [Fact]
    public void DefaultStream_IsAlerts() =>
        new ZulipChannelOptions().DefaultStream.ShouldBe("alerts");

    [Fact]
    public void DefaultTopic_IsSystem() =>
        new ZulipChannelOptions().DefaultTopic.ShouldBe("system");
}
