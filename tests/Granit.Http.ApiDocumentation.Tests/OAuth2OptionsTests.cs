using Granit.Http.ApiDocumentation.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class OAuth2OptionsTests
{
    [Fact]
    public void IsConfigured_BothUrlsSet_ReturnsTrue()
    {
        OAuth2Options options = new()
        {
            AuthorizationUrl = "https://keycloak.example.com/auth",
            TokenUrl = "https://keycloak.example.com/token",
        };

        options.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void IsConfigured_MissingAuthorizationUrl_ReturnsFalse()
    {
        OAuth2Options options = new()
        {
            TokenUrl = "https://keycloak.example.com/token",
        };

        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void IsConfigured_MissingTokenUrl_ReturnsFalse()
    {
        OAuth2Options options = new()
        {
            AuthorizationUrl = "https://keycloak.example.com/auth",
        };

        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_ScopesOpenId()
    {
        OAuth2Options options = new();

        options.Scopes.ShouldHaveSingleItem().ShouldBe("openid");
    }

    [Fact]
    public void Defaults_UrlsAreNull()
    {
        OAuth2Options options = new();

        options.AuthorizationUrl.ShouldBeNull();
        options.TokenUrl.ShouldBeNull();
        options.IsConfigured.ShouldBeFalse();
    }
}
