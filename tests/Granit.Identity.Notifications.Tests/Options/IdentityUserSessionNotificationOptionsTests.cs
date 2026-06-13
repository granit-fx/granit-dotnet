using Granit.Identity.Notifications.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Notifications.Tests.Options;

public sealed class IdentityUserSessionNotificationOptionsTests
{
    [Fact]
    public void SectionName_MatchesConvention() =>
        IdentityUserSessionNotificationOptions.SectionName.ShouldBe("Identity:Notifications:UserSessions");

    [Fact]
    public void BuildSecurityUrl_TrimsAndJoins()
    {
        var options = new IdentityUserSessionNotificationOptions
        {
            FrontendBaseUrl = "https://app.example.com/",
            SecurityPagePath = "account/security",
        };

        options.BuildSecurityUrl().ShouldBe("https://app.example.com/account/security");
    }
}
