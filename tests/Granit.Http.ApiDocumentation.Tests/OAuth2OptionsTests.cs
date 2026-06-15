using Granit.Http.ApiDocumentation.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class OAuth2OptionsTests
{
    [Fact]
    public void IsConfigured_AllSet_ReturnsTrue()
    {
        OAuth2Options options = new()
        {
            AuthorizationUrl = "https://keycloak.example.com/auth",
            TokenUrl = "https://keycloak.example.com/token",
            ClientId = "test-client",
        };

        options.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void IsConfigured_MissingAuthorizationUrl_ReturnsFalse()
    {
        OAuth2Options options = new()
        {
            TokenUrl = "https://keycloak.example.com/token",
            ClientId = "test-client",
        };

        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void IsConfigured_MissingTokenUrl_ReturnsFalse()
    {
        OAuth2Options options = new()
        {
            AuthorizationUrl = "https://keycloak.example.com/auth",
            ClientId = "test-client",
        };

        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void IsConfigured_MissingClientId_ReturnsFalse()
    {
        OAuth2Options options = new()
        {
            AuthorizationUrl = "https://keycloak.example.com/auth",
            TokenUrl = "https://keycloak.example.com/token",
        };

        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_EnablePkceTrue_ScopesOpenId()
    {
        OAuth2Options options = new();

        options.EnablePkce.ShouldBeTrue();
        options.Scopes.ShouldHaveSingleItem().ShouldBe("openid");
    }

    [Fact]
    public void Defaults_UrlsAndClientIdAreNull()
    {
        OAuth2Options options = new();

        options.AuthorizationUrl.ShouldBeNull();
        options.TokenUrl.ShouldBeNull();
        options.ClientId.ShouldBeNull();
        options.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_RedirectUriIsNull() =>
        // null means "let Scalar apply its (currently broken) default";
        // consumers must set this explicitly until upstream #8165/#8187 ship.
        new OAuth2Options().RedirectUri.ShouldBeNull();

    [Fact]
    public void RedirectUri_Roundtrips()
    {
        OAuth2Options options = new() { RedirectUri = "http://localhost:5000/scalar" };

        options.RedirectUri.ShouldBe("http://localhost:5000/scalar");
    }
}
