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
    public void Authority_CanBeSet()
    {
        JwtBearerAuthOptions options = new() { Authority = "https://idp.example.com" };

        options.Authority.ShouldBe("https://idp.example.com");
    }

    [Fact]
    public void Audience_CanBeSet()
    {
        JwtBearerAuthOptions options = new() { Audience = "my-api" };

        options.Audience.ShouldBe("my-api");
    }

    [Fact]
    public void NameClaimType_CanBeOverridden()
    {
        JwtBearerAuthOptions options = new() { NameClaimType = "preferred_username" };

        options.NameClaimType.ShouldBe("preferred_username");
    }

    [Fact]
    public void RequireHttpsMetadata_CanBeDisabled()
    {
        JwtBearerAuthOptions options = new() { RequireHttpsMetadata = false };

        options.RequireHttpsMetadata.ShouldBeFalse();
    }

    [Fact]
    public void BackChannelLogout_DefaultIsDisabled()
    {
        JwtBearerAuthOptions options = new();

        options.BackChannelLogout.Enabled.ShouldBeFalse();
    }
}
