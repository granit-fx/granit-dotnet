using System.Security.Claims;
using Granit.Authentication.JwtBearer.Authentication;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests;

public sealed class CurrentUserServiceAdditionalTests
{
    [Fact]
    public void FirstName_WithGivenNameClaim_ReturnsFirstName()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"),
            new Claim(ClaimTypes.GivenName, "Jean"));

        sut.FirstName.ShouldBe("Jean");
    }

    [Fact]
    public void FirstName_WithOidcGivenNameClaim_ReturnsFirstName()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"),
            new Claim("given_name", "Jean"));

        sut.FirstName.ShouldBe("Jean");
    }

    [Fact]
    public void FirstName_WithoutGivenNameClaim_ReturnsNull()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"));

        sut.FirstName.ShouldBeNull();
    }

    [Fact]
    public void LastName_WithSurnameClaim_ReturnsLastName()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"),
            new Claim(ClaimTypes.Surname, "Dupont"));

        sut.LastName.ShouldBe("Dupont");
    }

    [Fact]
    public void LastName_WithOidcFamilyNameClaim_ReturnsLastName()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"),
            new Claim("family_name", "Dupont"));

        sut.LastName.ShouldBe("Dupont");
    }

    [Fact]
    public void LastName_WithoutSurnameClaim_ReturnsNull()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"));

        sut.LastName.ShouldBeNull();
    }

    [Fact]
    public void Email_WithOidcEmailClaim_ReturnsEmail()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"),
            new Claim("email", "jean@example.com"));

        sut.Email.ShouldBe("jean@example.com");
    }

    [Fact]
    public void Email_WithoutEmailClaim_ReturnsNull()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"));

        sut.Email.ShouldBeNull();
    }

    [Fact]
    public void UserId_WithNameIdentifierClaim_ReturnsNameIdentifier()
    {
        CurrentUserService sut = CreateService(
            new Claim(ClaimTypes.NameIdentifier, "user-id-from-name-identifier"),
            new Claim("sub", "user-id-from-sub"));

        // NameIdentifier takes precedence over sub
        sut.UserId.ShouldBe("user-id-from-name-identifier");
    }

    [Fact]
    public void IsInRole_WithoutHttpContext_ReturnsFalse()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        CurrentUserService sut = new(accessor);

        sut.IsInRole("admin").ShouldBeFalse();
    }

    [Fact]
    public void GetRoles_WithNoRoleClaims_ReturnsEmpty()
    {
        CurrentUserService sut = CreateService(
            new Claim("sub", "user-123"));

        sut.GetRoles().ShouldBeEmpty();
    }

    private static CurrentUserService CreateService(params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return new CurrentUserService(accessor);
    }
}
