using Granit.Identity.Federated.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityGoogleCloud() => GoogleCloudIdentityOptions.SectionName.ShouldBe("Identity:Federated:GoogleCloud");

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
}
