using System.Security.Claims;
using Granit.Authentication.JwtBearer.Keycloak.Authentication;
using Shouldly;
using Xunit;
using KeycloakOptions = Granit.Authentication.JwtBearer.Keycloak.Options.KeycloakOptions;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Granit.Authentication.JwtBearer.Keycloak.Tests;

public sealed class KeycloakClaimsTransformationAdditionalTests
{
    private static KeycloakClaimsTransformation CreateSut(
        string roleClaimsSource = "realm_access",
        string clientId = "test-client") =>
        new(OptionsFactory.Create(new KeycloakOptions
        {
            RoleClaimsSource = roleClaimsSource,
            ClientId = clientId,
        }));

    [Fact]
    public async Task TransformAsync_ResourceAccess_MissingClientId_ReturnsUnmodified()
    {
        // Arrange — resource_access with a different client ID than configured
        string resourceAccess = """{"other-client":{"roles":["admin"]}}""";
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("resource_access", resourceAccess),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act
        ClaimsPrincipal result = await CreateSut(roleClaimsSource: "resource_access").TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_RealmAccess_NoRolesProperty_ReturnsUnmodified()
    {
        // Arrange — realm_access without "roles" key
        string realmAccess = """{"something_else":["admin"]}""";
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("realm_access", realmAccess),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_ResourceAccess_WithEmptyClientId_ReturnsUnmodified()
    {
        // Arrange — resource_access with empty ClientId configured
        string resourceAccess = """{"test-client":{"roles":["admin"]}}""";
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("resource_access", resourceAccess),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act — empty ClientId should not descend into any client node
        ClaimsPrincipal result = await CreateSut(roleClaimsSource: "resource_access", clientId: "").TransformAsync(principal);

        // Assert — should try to get "roles" from root, which has no direct "roles" key
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_NullRoleValue_IsSkipped()
    {
        // Arrange — roles array contains a null entry (unusual but possible)
        string realmAccess = """{"roles":["admin",null,"editor"]}""";
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("realm_access", realmAccess),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert — null values should be skipped
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("editor").ShouldBeTrue();
    }
}
