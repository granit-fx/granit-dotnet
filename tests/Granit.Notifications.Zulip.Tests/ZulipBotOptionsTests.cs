using Granit.Notifications.Zulip.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Zulip.Tests;

public sealed class ZulipBotOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        ZulipBotOptions.SectionName.ShouldBe("Notifications:Zulip:Bot");

    [Fact]
    public void Defaults_BaseUrlIsEmpty()
    {
        var options = new ZulipBotOptions();
        options.BaseUrl.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_BotEmailIsEmpty()
    {
        var options = new ZulipBotOptions();
        options.BotEmail.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_ApiKeyIsEmpty()
    {
        var options = new ZulipBotOptions();
        options.ApiKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_TimeoutIs30Seconds()
    {
        var options = new ZulipBotOptions();
        options.TimeoutSeconds.ShouldBe(30);
    }
}
