using System.Security.Claims;
using Granit.Authentication.EntraId.Authentication;
using Shouldly;
using Xunit;

namespace Granit.Authentication.EntraId.Tests;

public sealed class EntraIdClaimsTransformationAdditionalTests
{
    private static EntraIdClaimsTransformation CreateSut() => new();

    [Fact]
    public async Task TransformAsync_MalformedJsonArray_SkipsSilently()
    {
        // Arrange — roles claim with invalid JSON
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", "[not valid json"),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act — should not throw
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert — no roles should be added
        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_MixedV1AndV2Formats_HandlesCorrectly()
    {
        // Arrange — one v1 (JSON array) and one v2 (individual) claim
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", """["admin","editor"]"""),
                new Claim("roles", "viewer"),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(3);
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("editor").ShouldBeTrue();
        result.IsInRole("viewer").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_JsonArrayWithNullElements_SkipsNulls()
    {
        // Arrange — JSON array with null elements
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim("roles", """["admin",null,"editor"]"""),
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

    [Fact]
    public async Task TransformAsync_JsonArrayWithDuplicatesAcrossClaims_Deduplicates()
    {
        // Arrange — duplicate roles across v1 array and existing claims
        ClaimsIdentity identity = new(
            [
                new Claim("sub", "user-123"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim("roles", """["admin","editor"]"""),
                new Claim("roles", "admin"),
            ],
            "Bearer");
        ClaimsPrincipal principal = new(identity);

        // Act
        ClaimsPrincipal result = await CreateSut().TransformAsync(principal);

        // Assert — "admin" should appear only once
        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "admin").ShouldBe(1);
        result.FindAll(ClaimTypes.Role).Count().ShouldBe(2); // admin + editor
    }
}
