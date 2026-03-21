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

    [Fact]
    public void Enabled_CanBeSet()
    {
        BackChannelLogoutOptions options = new() { Enabled = true };

        options.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void EndpointPath_CanBeOverridden()
    {
        BackChannelLogoutOptions options = new() { EndpointPath = "/custom/logout" };

        options.EndpointPath.ShouldBe("/custom/logout");
    }

    [Fact]
    public void SessionRevocationTtl_CanBeOverridden()
    {
        var customTtl = TimeSpan.FromMinutes(30);
        BackChannelLogoutOptions options = new() { SessionRevocationTtl = customTtl };

        options.SessionRevocationTtl.ShouldBe(customTtl);
    }
}
