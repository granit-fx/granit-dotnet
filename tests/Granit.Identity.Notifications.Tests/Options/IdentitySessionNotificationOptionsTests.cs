using Granit.Identity.Notifications.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Notifications.Tests.Options;

public sealed class IdentitySessionNotificationOptionsTests
{
    [Fact]
    public void SectionName_MatchesConvention() =>
        IdentitySessionNotificationOptions.SectionName.ShouldBe("Identity:Notifications:Sessions");

    [Fact]
    public void BuildSecurityUrl_TrimsAndJoins()
    {
        var options = new IdentitySessionNotificationOptions
        {
            FrontendBaseUrl = "https://app.example.com/",
            SecurityPagePath = "account/security",
        };

        options.BuildSecurityUrl().ShouldBe("https://app.example.com/account/security");
    }
}
