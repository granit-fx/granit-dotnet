using System.Security.Claims;
using Granit.Authentication.Claims;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Tests.Claims;

public sealed class OpenIddictRoleClaimsTransformationTests
{
    private const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";

    [Fact]
    public async Task TransformAsync_WithOpenIddictValidationScheme_CopiesShortRoleClaimsToClaimTypesRole()
    {
        // Arrange — OpenIddict.Validation emits roles under the OIDC short claim type "role".
        ClaimsPrincipal principal = CreatePrincipal(
            OpenIddictValidationScheme,
            new Claim("role", "SuperAdmin"),
            new Claim("role", "Auditor"));

        OpenIddictRoleClaimsTransformation sut = new();

        // Act
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert — both roles now also exist under ClaimTypes.Role.
        result.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["SuperAdmin", "Auditor"]);
        result.IsInRole("SuperAdmin").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithoutOpenIddictValidationScheme_LeavesPrincipalUnchanged()
    {
        // Arrange — cookie or other scheme: transformation must NOT mutate.
        ClaimsPrincipal principal = CreatePrincipal(
            "Cookies",
            new Claim("role", "X"));

        OpenIddictRoleClaimsTransformation sut = new();

        // Act
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_AcrossMultipleInvocations()
    {
        // Arrange — IClaimsTransformation may be invoked more than once per request.
        ClaimsPrincipal principal = CreatePrincipal(
            OpenIddictValidationScheme,
            new Claim("role", "SuperAdmin"));

        OpenIddictRoleClaimsTransformation sut = new();

        // Act — invoke twice.
        await sut.TransformAsync(principal);
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert — exactly one ClaimTypes.Role/SuperAdmin, not two.
        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "SuperAdmin").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_WhenClaimTypesRoleAlreadyPresent_DoesNotDuplicate()
    {
        // Arrange — JwtBearer-mapped principal already carries ClaimTypes.Role/X plus
        // a stray "role" claim with the same value (rare but plausible).
        ClaimsPrincipal principal = CreatePrincipal(
            OpenIddictValidationScheme,
            new Claim("role", "Admin"),
            new Claim(ClaimTypes.Role, "Admin"));

        OpenIddictRoleClaimsTransformation sut = new();

        // Act
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert — exactly one ClaimTypes.Role/Admin, no duplication.
        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "Admin").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_WithUnauthenticatedIdentity_LeavesPrincipalUnchanged()
    {
        // Arrange — anonymous principal (no AuthenticationType).
        ClaimsIdentity identity = new();
        ClaimsPrincipal principal = new(identity);

        OpenIddictRoleClaimsTransformation sut = new();

        // Act
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithNullPrincipal_Throws()
    {
        OpenIddictRoleClaimsTransformation sut = new();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.TransformAsync(null!));
    }

    [Fact]
    public async Task TransformAsync_WithNoRoleClaims_DoesNotMutate()
    {
        // Arrange — authenticated OpenIddict principal carrying only sub/email.
        ClaimsPrincipal principal = CreatePrincipal(
            OpenIddictValidationScheme,
            new Claim("sub", "user-123"),
            new Claim("email", "u@example.com"));

        OpenIddictRoleClaimsTransformation sut = new();

        // Act
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    private static ClaimsPrincipal CreatePrincipal(string authenticationType, params Claim[] claims)
    {
        // RoleClaimType "role" mirrors what OpenIddict.Validation produces (OIDC short claim).
        ClaimsIdentity identity = new(claims, authenticationType, ClaimTypes.NameIdentifier, roleType: "role");
        return new ClaimsPrincipal(identity);
    }
}
