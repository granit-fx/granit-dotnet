using Granit.Notifications.Sms.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class SmsChannelOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        SmsChannelOptions.SectionName.ShouldBe("Notifications:Sms");

    [Fact]
    public void Defaults_ProviderIsEmpty()
    {
        SmsChannelOptions options = new();

        options.Provider.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_SenderIdIsNull()
    {
        SmsChannelOptions options = new();

        options.SenderId.ShouldBeNull();
    }
}
