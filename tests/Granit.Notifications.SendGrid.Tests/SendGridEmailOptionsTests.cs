using Granit.Notifications.SendGrid.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SendGrid.Tests;

public sealed class SendGridEmailOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        SendGridEmailOptions.SectionName.ShouldBe("Notifications:SendGrid");

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        SendGridEmailOptions options = new();

        options.ApiKey.ShouldBe(string.Empty);
        options.DefaultSenderEmail.ShouldBe(string.Empty);
        options.DefaultSenderName.ShouldBe(string.Empty);
        options.BaseUrl.ShouldBe("https://api.sendgrid.com/v3");
        options.TimeoutSeconds.ShouldBe(30);
    }
}
