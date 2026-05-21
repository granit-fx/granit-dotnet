using Granit.Authentication.JwtBearer.Keycloak.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Keycloak.Tests;

public sealed class KeycloakOptionsTests
{
    [Fact]
    public void SectionName_IsAuthenticationKeycloak() =>
        KeycloakOptions.SectionName.ShouldBe("Authentication:Keycloak");

    [Fact]
    public void Defaults_AreCorrect()
    {
        KeycloakOptions options = new();

        options.Authority.ShouldBe(string.Empty);
        options.ClientId.ShouldBe(string.Empty);
        options.ClientSecret.ShouldBe(string.Empty);
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.Audience.ShouldBeNull();
        options.RoleClaimsSource.ShouldBe("realm_access");
    }

    [Fact]
    public void RoleClaimsSource_CanBeSetToResourceAccess()
    {
        KeycloakOptions options = new() { RoleClaimsSource = "resource_access" };

        options.RoleClaimsSource.ShouldBe("resource_access");
    }

    [Fact]
    public void Audience_WhenNull_DefaultsToNull()
    {
        KeycloakOptions options = new();

        options.Audience.ShouldBeNull();
    }

    [Fact]
    public void Audience_CanBeSetExplicitly()
    {
        KeycloakOptions options = new() { Audience = "custom-audience" };

        options.Audience.ShouldBe("custom-audience");
    }
}
