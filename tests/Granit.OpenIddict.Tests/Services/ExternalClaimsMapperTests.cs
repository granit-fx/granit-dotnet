using System.Security.Claims;
using Granit.OpenIddict.Services;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Services;

public sealed class ExternalClaimsMapperTests
{
    private readonly ExternalClaimsMapper _mapper = new();

    [Fact]
    public void Maps_Email_From_ClaimTypes_Email()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "alice@test.com"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.Email.ShouldBe("alice@test.com");
        result.UserName.ShouldBe("alice@test.com");
    }

    [Fact]
    public void Maps_Email_From_Oidc_Email_Claim()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("email", "bob@test.com"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.Email.ShouldBe("bob@test.com");
        result.UserName.ShouldBe("bob@test.com");
    }

    [Fact]
    public void Prefers_ClaimTypes_Email_Over_Oidc_Email()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "preferred@test.com"),
            new Claim("email", "fallback@test.com"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.Email.ShouldBe("preferred@test.com");
    }

    [Fact]
    public void Maps_FirstName_From_ClaimTypes_GivenName()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.GivenName, "Alice"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.FirstName.ShouldBe("Alice");
    }

    [Fact]
    public void Maps_FirstName_From_Oidc_GivenName()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("given_name", "Bob"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.FirstName.ShouldBe("Bob");
    }

    [Fact]
    public void Maps_LastName_From_ClaimTypes_Surname()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.Surname, "Doe"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.LastName.ShouldBe("Doe");
    }

    [Fact]
    public void Maps_LastName_From_Oidc_FamilyName()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("family_name", "Smith"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.LastName.ShouldBe("Smith");
    }

    [Fact]
    public void Returns_Null_Properties_When_No_Claims_Present()
    {
        ClaimsPrincipal principal = CreatePrincipal();

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Google");

        result.Email.ShouldBeNull();
        result.FirstName.ShouldBeNull();
        result.LastName.ShouldBeNull();
        result.UserName.ShouldBeNull();
    }

    [Fact]
    public void Maps_All_Properties_When_All_Claims_Present()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.Email, "alice@test.com"),
            new Claim(ClaimTypes.GivenName, "Alice"),
            new Claim(ClaimTypes.Surname, "Doe"));

        ExternalUserProperties result = _mapper.MapToUserProperties(principal, "Microsoft");

        result.Email.ShouldBe("alice@test.com");
        result.FirstName.ShouldBe("Alice");
        result.LastName.ShouldBe("Doe");
        result.UserName.ShouldBe("alice@test.com");
    }

    [Fact]
    public void Throws_On_Null_Principal()
    {
        Should.Throw<ArgumentNullException>(() =>
            _mapper.MapToUserProperties(null!, "Google"));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));
}
