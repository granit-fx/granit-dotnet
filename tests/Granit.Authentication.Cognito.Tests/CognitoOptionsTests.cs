using Granit.Authentication.Cognito.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Cognito.Tests;

public sealed class CognitoOptionsTests
{
    [Fact]
    public void SectionName_IsCognito() =>
        CognitoOptions.SectionName.ShouldBe("Cognito");

    [Fact]
    public void Defaults_AreCorrect()
    {
        CognitoOptions options = new();

        options.Authority.ShouldBe(string.Empty);
        options.ClientId.ShouldBe(string.Empty);
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.Audience.ShouldBeNull();
        options.AdminGroup.ShouldBe("admin");
    }

    [Fact]
    public void Audience_WhenNull_DefaultsToNull()
    {
        CognitoOptions options = new();

        options.Audience.ShouldBeNull();
    }

    [Fact]
    public void Audience_CanBeSetExplicitly()
    {
        CognitoOptions options = new() { Audience = "custom-audience" };

        options.Audience.ShouldBe("custom-audience");
    }

    [Fact]
    public void AdminGroup_CanBeOverridden()
    {
        CognitoOptions options = new() { AdminGroup = "super-admins" };

        options.AdminGroup.ShouldBe("super-admins");
    }
}
