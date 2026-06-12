using Granit.UserSessions.Notifications.Options;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Options;

public sealed class UserSessionsNotificationOptionsTests
{
    [Fact]
    public void SectionName_MatchesConvention() =>
        UserSessionsNotificationOptions.SectionName.ShouldBe("UserSessions:Notifications");

    [Fact]
    public void BuildSecurityUrl_TrimsAndJoins()
    {
        var options = new UserSessionsNotificationOptions
        {
            FrontendBaseUrl = "https://app.example.com/",
            SecurityPagePath = "account/security",
        };

        options.BuildSecurityUrl().ShouldBe("https://app.example.com/account/security");
    }
}
