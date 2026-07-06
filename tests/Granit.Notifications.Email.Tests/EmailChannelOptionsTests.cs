using Granit.Notifications.Email.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailChannelOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        EmailChannelOptions.SectionName.ShouldBe("Notifications:Email");

    [Fact]
    public void Defaults_ProviderIsSmtp()
    {
        EmailChannelOptions options = new();

        options.Provider.ShouldBe("Smtp");
    }

    [Fact]
    public void Defaults_DefaultSenderEmailIsEmpty()
    {
        EmailChannelOptions options = new();

        options.DefaultSenderEmail.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_DefaultSenderNameIsEmpty()
    {
        EmailChannelOptions options = new();

        options.DefaultSenderName.ShouldBe(string.Empty);
    }
}
