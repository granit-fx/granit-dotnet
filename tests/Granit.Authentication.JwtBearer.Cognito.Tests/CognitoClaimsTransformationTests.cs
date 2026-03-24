using System.Security.Claims;
using Granit.Authentication.JwtBearer.Cognito.Authentication;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Cognito.Tests;

public sealed class CognitoClaimsTransformationTests
{
    private static CognitoClaimsTransformation CreateSut() => new();

    [Fact]
    public async Task TransformAsync_WithCognitoGroups_MapsToRoleClaims()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new("Bearer");
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        identity.AddClaim(new Claim("cognito:groups", "users"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("users").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithoutGroups_ReturnsUnchanged()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new("Bearer");
        identity.AddClaim(new Claim("sub", "user-123"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_Unauthenticated_ReturnsUnchanged()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new(); // no authenticationType = unauthenticated
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_DuplicateGroups_DoesNotAddDuplicateRoles()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new("Bearer");
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count().ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_ExistingRoleClaims_DoesNotDuplicate()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new("Bearer");
        identity.AddClaim(new Claim(ClaimTypes.Role, "admin"));
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        identity.AddClaim(new Claim("cognito:groups", "users"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("users").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_EmptyGroupValue_IsIgnored()
    {
        CognitoClaimsTransformation sut = CreateSut();
        ClaimsIdentity identity = new("Bearer");
        identity.AddClaim(new Claim("cognito:groups", ""));
        identity.AddClaim(new Claim("cognito:groups", "admin"));
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count().ShouldBe(1);
        result.IsInRole("admin").ShouldBeTrue();
    }
}
