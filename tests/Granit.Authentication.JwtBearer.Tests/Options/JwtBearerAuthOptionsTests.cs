using Granit.Authentication.JwtBearer.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests.Options;

public sealed class JwtBearerAuthOptionsTests
{
    [Fact]
    public void SectionName_IsAuthentication() =>
        JwtBearerAuthOptions.SectionName.ShouldBe("Authentication");

    [Fact]
    public void Defaults_AreCorrect()
    {
        JwtBearerAuthOptions options = new();

        options.Authority.ShouldBe(string.Empty);
        options.Audience.ShouldBe(string.Empty);
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.NameClaimType.ShouldBe("sub");
        options.BackChannelLogout.ShouldNotBeNull();
    }

    [Fact]
    public void BackChannelLogout_DefaultIsDisabled()
    {
        JwtBearerAuthOptions options = new();

        options.BackChannelLogout.Enabled.ShouldBeFalse();
    }
}
