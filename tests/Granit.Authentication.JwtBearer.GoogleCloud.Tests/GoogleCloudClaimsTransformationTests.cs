using System.Security.Claims;
using Granit.Authentication.JwtBearer.GoogleCloud.Authentication;
using Granit.Authentication.JwtBearer.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Tests;

public sealed class GoogleCloudClaimsTransformationTests
{
    private static GoogleCloudClaimsTransformation CreateSut(string rolesClaimKey = "roles") =>
        new(Microsoft.Extensions.Options.Options.Create(new GoogleCloudAuthenticationOptions { RolesClaimKey = rolesClaimKey }));

    [Fact]
    public async Task TransformAsync_MapsJsonArrayRolesToClaimTypesRole()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim("roles", """["admin","editor"]"""),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["admin", "editor"], ignoreOrder: true);
    }

    [Fact]
    public async Task TransformAsync_MapsSingleStringRole()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim("roles", "admin"),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldContain("admin");
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateExistingRoles()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("roles", """["admin","editor"]"""),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).Count(c => c.Value == "admin").ShouldBe(1);
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldContain("editor");
    }

    [Fact]
    public async Task TransformAsync_SkipsUnauthenticatedPrincipal()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("roles", """["admin"]"""),
        ]); // no authentication type → not authenticated
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_SkipsWhenNoRolesClaim()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_UsesConfiguredRolesClaimKey()
    {
        GoogleCloudClaimsTransformation sut = CreateSut("custom_roles");
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim("custom_roles", """["viewer"]"""),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldContain("viewer");
    }

    [Fact]
    public async Task TransformAsync_HandlesMalformedJson()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim("roles", "[not valid json"),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Should not throw
        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_HandlesEmptyJsonArray()
    {
        GoogleCloudClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(
        [
            new Claim("sub", "uid-1"),
            new Claim("roles", "[]"),
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);

        await sut.TransformAsync(principal);

        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }
}
