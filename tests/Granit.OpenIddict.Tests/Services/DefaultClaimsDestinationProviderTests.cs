using System.Security.Claims;
using Granit.OpenIddict.Services;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Services;

public sealed class DefaultClaimsDestinationProviderTests
{
    private readonly DefaultClaimsDestinationProvider _provider = new();

    [Fact]
    public void Sub_Claim_Returns_Both_Destinations()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("sub", "user-123");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
        destinations.Count.ShouldBe(2);
    }

    [Fact]
    public void NameIdentifier_Claim_Returns_Both_Destinations()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.NameIdentifier, "user-123");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
        destinations.Count.ShouldBe(2);
    }

    [Fact]
    public void Name_Claim_Returns_AccessToken_Only_Without_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.Name, "Alice");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Name_Claim_Returns_Both_With_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("profile");
        var claim = new Claim(ClaimTypes.Name, "Alice");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void GivenName_Claim_Returns_AccessToken_Only_Without_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.GivenName, "Alice");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void GivenName_Claim_Returns_Both_With_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("profile");
        var claim = new Claim(ClaimTypes.GivenName, "Alice");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void FamilyName_Claim_Returns_AccessToken_Only_Without_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.Surname, "Doe");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void FamilyName_Claim_Returns_Both_With_Profile_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("profile");
        var claim = new Claim(ClaimTypes.Surname, "Doe");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Email_Claim_Returns_AccessToken_Only_Without_Email_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.Email, "alice@test.com");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Email_Claim_Returns_Both_With_Email_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("email");
        var claim = new Claim(ClaimTypes.Email, "alice@test.com");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Role_Claim_Returns_AccessToken_Only_Without_Roles_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim(ClaimTypes.Role, "admin");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Role_Claim_Returns_Both_With_Roles_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("roles");
        var claim = new Claim(ClaimTypes.Role, "admin");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void SecurityStamp_Claim_Returns_Empty_Destinations()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("AspNet.Identity.SecurityStamp", "stamp-value");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldBeEmpty();
    }

    [Fact]
    public void SecurityStamp_ShortName_Returns_Empty_Destinations()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("security_stamp", "stamp-value");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldBeEmpty();
    }

    [Fact]
    public void Unknown_Claim_Returns_AccessToken_Only()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("custom_claim", "some-value");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldBe([ClaimsDestinations.AccessToken]);
    }

    [Fact]
    public void Null_Principal_Throws_ArgumentNullException()
    {
        var claim = new Claim("sub", "user-123");

        Should.Throw<ArgumentNullException>(() =>
            _provider.GetDestinations(claim, null!).ToList());
    }

    [Fact]
    public void Null_Claim_Throws_ArgumentNullException()
    {
        ClaimsPrincipal principal = CreatePrincipal();

        Should.Throw<ArgumentNullException>(() =>
            _provider.GetDestinations(null!, principal).ToList());
    }

    [Fact]
    public void OidcEmail_Claim_Returns_AccessToken_Only_Without_Email_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("email", "alice@test.com");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void OidcEmail_Claim_Returns_Both_With_Email_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("email");
        var claim = new Claim("email", "alice@test.com");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Phone_Claim_Returns_AccessToken_Only_Without_Phone_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipal();
        var claim = new Claim("phone_number", "+1234567890");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldNotContain(ClaimsDestinations.IdentityToken);
    }

    [Fact]
    public void Phone_Claim_Returns_Both_With_Phone_Scope()
    {
        ClaimsPrincipal principal = CreatePrincipalWithScopes("phone");
        var claim = new Claim("phone_number", "+1234567890");

        var destinations = _provider.GetDestinations(claim, principal).ToList();

        destinations.ShouldContain(ClaimsDestinations.AccessToken);
        destinations.ShouldContain(ClaimsDestinations.IdentityToken);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    private static ClaimsPrincipal CreatePrincipalWithScopes(params string[] scopes)
    {
        var claims = scopes.Select(s => new Claim("oi_scp", s)).ToList();
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
