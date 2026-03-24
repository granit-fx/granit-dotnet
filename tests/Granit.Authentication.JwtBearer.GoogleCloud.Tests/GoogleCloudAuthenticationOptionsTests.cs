using Granit.Authentication.JwtBearer.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GoogleCloudAuthenticationOptionsTests
{
    [Fact]
    public void SectionName_IsGoogleCloudAuth() =>
        GoogleCloudAuthenticationOptions.SectionName.ShouldBe("GoogleCloudAuth");

    [Fact]
    public void Authority_IsDerivedFromProjectId()
    {
        GoogleCloudAuthenticationOptions opts = new() { ProjectId = "my-project" };

        opts.Authority.ShouldBe("https://securetoken.google.com/my-project");
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        GoogleCloudAuthenticationOptions opts = new();

        opts.RequireHttpsMetadata.ShouldBeTrue();
        opts.AdminRole.ShouldBe("admin");
        opts.RolesClaimKey.ShouldBe("roles");
    }
}
