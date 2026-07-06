using Granit.Authentication.JwtBearer.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests.Options;

public sealed class BackChannelLogoutOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        BackChannelLogoutOptions options = new();

        options.Enabled.ShouldBeFalse();
        options.EndpointPath.ShouldBe("/auth/back-channel-logout");
        options.SessionRevocationTtl.ShouldBe(TimeSpan.FromHours(1));
    }
}
