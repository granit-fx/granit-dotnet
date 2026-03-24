// =============================================================================
// Tests - EntraIdClaimsTransformation
// =============================================================================
// Verifies that Entra ID App Roles are correctly mapped to ClaimTypes.Role.
// Covers both v2.0 (individual claims) and v1.0 (JSON array) formats.
// =============================================================================

using System.Security.Claims;
using Granit.Authentication.JwtBearer.EntraId.Authentication;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.EntraId.Tests;

public sealed class EntraIdClaimsTransformationTests
{
    private static EntraIdClaimsTransformation CreateSut() => new();

    [Fact]
    public async Task TransformAsync_WithIndividualRoleClaims_AddsRoleClaims()
    {
        // Arrange — v2.0 format: individual "roles" claims
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", "admin"),
                new Claim("roles", "editor")
            ],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("editor").ShouldBeTrue();
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
    }

    [Fact]
    public async Task TransformAsync_WithJsonArrayRoleClaim_AddsRoleClaims()
    {
        // Arrange — v1.0 format: single "roles" claim with JSON array
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", """["admin","editor"]""")
            ],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("editor").ShouldBeTrue();
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
    }

    [Fact]
    public async Task TransformAsync_WithoutRolesClaim_ReturnsUnmodifiedPrincipal()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            [new Claim("sub", "user-123")],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithUnauthenticatedPrincipal_ReturnsUnmodifiedPrincipal()
    {
        // Arrange — no AuthenticationType → IsAuthenticated = false
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.Identity!.IsAuthenticated.ShouldBeFalse();
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithEmptyJsonArray_AddsNoClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", "[]")
            ],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateExistingRoles()
    {
        // Arrange — existing ClaimTypes.Role + matching "roles" claim
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim("roles", "admin")
            ],
            "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(1);
    }
}
