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
    public void Defaults_SenderAddressIsEmpty()
    {
        EmailChannelOptions options = new();

        options.SenderAddress.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_SenderNameIsEmpty()
    {
        EmailChannelOptions options = new();

        options.SenderName.ShouldBe(string.Empty);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        EmailChannelOptions options = new()
        {
            Provider = "Brevo",
            SenderAddress = "no-reply@example.com",
            SenderName = "My App",
        };

        options.Provider.ShouldBe("Brevo");
        options.SenderAddress.ShouldBe("no-reply@example.com");
        options.SenderName.ShouldBe("My App");
    }
}
