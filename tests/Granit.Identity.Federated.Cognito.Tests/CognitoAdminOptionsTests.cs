using Granit.Identity.Federated.Cognito.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class CognitoAdminOptionsTests
{
    [Fact]
    public void SectionName_IsCognitoAdmin() => CognitoAdminOptions.SectionName.ShouldBe("Identity:Federated:Cognito");

    [Fact]
    public void DefaultValues_AreEmptyStrings()
    {
        CognitoAdminOptions options = new();

        options.Region.ShouldBe(string.Empty);
        options.UserPoolId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_AppClientIdIsNull()
    {
        CognitoAdminOptions options = new();

        options.AppClientId.ShouldBeNull();
    }

    [Fact]
    public void Defaults_AccessKeyIdIsNull()
    {
        CognitoAdminOptions options = new();

        options.AccessKeyId.ShouldBeNull();
    }

    [Fact]
    public void Defaults_SecretAccessKeyIsNull()
    {
        CognitoAdminOptions options = new();

        options.SecretAccessKey.ShouldBeNull();
    }

    [Fact]
    public void Defaults_TimeoutSecondsIs30()
    {
        CognitoAdminOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        CognitoAdminOptions options = new()
        {
            Region = "eu-west-1",
            UserPoolId = "eu-west-1_XXXXXXXXX",
            AppClientId = "app-client-id",
            AccessKeyId = "AKIA_KEY",
            SecretAccessKey = "secret-key",
            TimeoutSeconds = 60,
        };

        options.Region.ShouldBe("eu-west-1");
        options.UserPoolId.ShouldBe("eu-west-1_XXXXXXXXX");
        options.AppClientId.ShouldBe("app-client-id");
        options.AccessKeyId.ShouldBe("AKIA_KEY");
        options.SecretAccessKey.ShouldBe("secret-key");
        options.TimeoutSeconds.ShouldBe(60);
    }
}
