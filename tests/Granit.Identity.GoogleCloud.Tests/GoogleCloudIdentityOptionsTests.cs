using Granit.Identity.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityGoogleCloud() => GoogleCloudIdentityOptions.SectionName.ShouldBe("Identity:GoogleCloud");

    [Fact]
    public void Defaults_ProjectIdIsEmpty()
    {
        GoogleCloudIdentityOptions options = new();

        options.ProjectId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_CredentialFilePathIsNull()
    {
        GoogleCloudIdentityOptions options = new();

        options.CredentialFilePath.ShouldBeNull();
    }

    [Fact]
    public void Defaults_RolesClaimKeyIsRoles()
    {
        GoogleCloudIdentityOptions options = new();

        options.RolesClaimKey.ShouldBe("roles");
    }

    [Fact]
    public void Defaults_TimeoutSecondsIs30()
    {
        GoogleCloudIdentityOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        GoogleCloudIdentityOptions options = new()
        {
            ProjectId = "my-gcp-project",
            CredentialFilePath = "/etc/keys/service-account.json",
            RolesClaimKey = "custom_roles",
            TimeoutSeconds = 60,
        };

        options.ProjectId.ShouldBe("my-gcp-project");
        options.CredentialFilePath.ShouldBe("/etc/keys/service-account.json");
        options.RolesClaimKey.ShouldBe("custom_roles");
        options.TimeoutSeconds.ShouldBe(60);
    }
}
