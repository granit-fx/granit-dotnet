using System.Security.Claims;
using Granit.OpenIddict.Extensions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void IsImpersonated_ReturnsFalse_WhenNoImpersonatorClaim()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        principal.IsImpersonated().ShouldBeFalse();
    }

    [Fact]
    public void IsImpersonated_ReturnsTrue_WhenImpersonatorClaimPresent()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("impersonator_id", "admin-123"));
        principal.IsImpersonated().ShouldBeTrue();
    }

    [Fact]
    public void FindImpersonatorUserId_ReturnsNull_WhenNotImpersonating()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        principal.FindImpersonatorUserId().ShouldBeNull();
    }

    [Fact]
    public void FindImpersonatorUserId_ReturnsId_WhenImpersonating()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("impersonator_id", "admin-456"));
        principal.FindImpersonatorUserId().ShouldBe("admin-456");
    }

    [Fact]
    public void FindImpersonatorName_ReturnsNull_WhenNotImpersonating()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        principal.FindImpersonatorName().ShouldBeNull();
    }

    [Fact]
    public void FindImpersonatorName_ReturnsName_WhenImpersonating()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("impersonator_name", "admin@example.com"));
        principal.FindImpersonatorName().ShouldBe("admin@example.com");
    }

    [Fact]
    public void IsImpersonated_ThrowsOnNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            ((ClaimsPrincipal)null!).IsImpersonated());
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));
}
