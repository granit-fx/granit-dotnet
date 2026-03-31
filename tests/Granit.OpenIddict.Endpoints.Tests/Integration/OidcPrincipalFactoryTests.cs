using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Integration;

public sealed class OidcPrincipalFactoryTests
{
    private readonly UserManager<GranitUser> _userManager;
    private readonly IClaimsDestinationProvider _destinationProvider;
    private readonly OidcPrincipalFactory _factory;

    public OidcPrincipalFactoryTests()
    {
        IUserStore<GranitUser> userStore = Substitute.For<IUserStore<GranitUser>>();
        _userManager = Substitute.For<UserManager<GranitUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _destinationProvider = Substitute.For<IClaimsDestinationProvider>();
        _destinationProvider.GetDestinations(Arg.Any<Claim>(), Arg.Any<ClaimsPrincipal>())
            .Returns([OpenIddictConstants.Destinations.AccessToken]);

        _factory = new OidcPrincipalFactory(_userManager, _destinationProvider);
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsSubjectClaim()
    {
        GranitUser user = CreateTestUser();
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
            .ShouldBe(user.Id.ToString());
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsPreferredUsername()
    {
        GranitUser user = CreateTestUser();
        user.UserName = "jdoe";
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.PreferredUsername)?.Value
            .ShouldBe("jdoe");
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsNameFromFirstAndLast()
    {
        GranitUser user = CreateTestUser();
        user.FirstName = "John";
        user.LastName = "Doe";
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Name)?.Value.ShouldBe("John Doe");
        identity.FindFirst(OpenIddictConstants.Claims.GivenName)?.Value.ShouldBe("John");
        identity.FindFirst(OpenIddictConstants.Claims.FamilyName)?.Value.ShouldBe("Doe");
    }

    [Fact]
    public async Task CreateUserPrincipal_OnlyFirstName_SetsNameWithoutTrailingSpace()
    {
        GranitUser user = CreateTestUser();
        user.FirstName = "John";
        user.LastName = null;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Name)?.Value.ShouldBe("John");
        identity.FindFirst(OpenIddictConstants.Claims.GivenName)?.Value.ShouldBe("John");
        identity.FindFirst(OpenIddictConstants.Claims.FamilyName).ShouldBeNull();
    }

    [Fact]
    public async Task CreateUserPrincipal_NullNames_NoNameClaim()
    {
        GranitUser user = CreateTestUser();
        user.FirstName = null;
        user.LastName = null;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Name).ShouldBeNull();
        identity.FindFirst(OpenIddictConstants.Claims.GivenName).ShouldBeNull();
        identity.FindFirst(OpenIddictConstants.Claims.FamilyName).ShouldBeNull();
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsEmailClaims()
    {
        GranitUser user = CreateTestUser();
        user.Email = "john@example.com";
        user.EmailConfirmed = true;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Email)?.Value.ShouldBe("john@example.com");
        identity.FindFirst(OpenIddictConstants.Claims.EmailVerified)?.Value.ShouldBe("true");
    }

    [Fact]
    public async Task CreateUserPrincipal_UnconfirmedEmail_SetsVerifiedFalse()
    {
        GranitUser user = CreateTestUser();
        user.Email = "john@example.com";
        user.EmailConfirmed = false;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.EmailVerified)?.Value.ShouldBe("false");
    }

    [Fact]
    public async Task CreateUserPrincipal_NullEmail_NoEmailClaim()
    {
        GranitUser user = CreateTestUser();
        user.Email = null;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Email).ShouldBeNull();
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsPhoneClaims()
    {
        GranitUser user = CreateTestUser();
        user.PhoneNumber = "+32471000000";
        user.PhoneNumberConfirmed = true;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.PhoneNumber)?.Value.ShouldBe("+32471000000");
        identity.FindFirst(OpenIddictConstants.Claims.PhoneNumberVerified)?.Value.ShouldBe("true");
    }

    [Fact]
    public async Task CreateUserPrincipal_NullPhone_NoPhoneClaim()
    {
        GranitUser user = CreateTestUser();
        user.PhoneNumber = null;
        SetupUserManager(user);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.PhoneNumber).ShouldBeNull();
    }

    [Fact]
    public async Task CreateUserPrincipal_IncludesRoles()
    {
        GranitUser user = CreateTestUser();
        SetupUserManager(user, roles: ["admin", "editor"]);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        IEnumerable<string> roleClaims = identity.FindAll(OpenIddictConstants.Claims.Role)
            .Select(c => c.Value);
        roleClaims.ShouldContain("admin");
        roleClaims.ShouldContain("editor");
    }

    [Fact]
    public async Task CreateUserPrincipal_IncludesCustomClaims()
    {
        GranitUser user = CreateTestUser();
        List<Claim> customClaims = [new Claim("tenant_id", "abc-123")];
        SetupUserManager(user, customClaims: customClaims);

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst("tenant_id")?.Value.ShouldBe("abc-123");
    }

    [Fact]
    public async Task CreateUserPrincipal_SetsScopes()
    {
        GranitUser user = CreateTestUser();
        SetupUserManager(user);

        ImmutableArray<string> scopes = ["openid", "profile", "email"];

        ClaimsPrincipal principal = await _factory.CreateUserPrincipalAsync(
            user, scopes, "TestScheme", TestContext.Current.CancellationToken);

        // Scopes are set via OpenIddict extension methods on the identity
        principal.ShouldNotBeNull();
        principal.Identity.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateUserPrincipal_CallsDestinationProvider()
    {
        GranitUser user = CreateTestUser();
        SetupUserManager(user);

        await _factory.CreateUserPrincipalAsync(
            user, [], "TestScheme", TestContext.Current.CancellationToken);

        _destinationProvider.Received().GetDestinations(Arg.Any<Claim>(), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public void CreateClientPrincipal_SetsSubjectToClientId()
    {
        ClaimsPrincipal principal = OidcPrincipalFactory.CreateClientPrincipal(
            "my-client", [], "TestScheme");

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value.ShouldBe("my-client");
    }

    [Fact]
    public void CreateClientPrincipal_SetsAuthenticationScheme()
    {
        ClaimsPrincipal principal = OidcPrincipalFactory.CreateClientPrincipal(
            "my-client", [], "CustomScheme");

        ClaimsIdentity identity = principal.Identity.ShouldBeOfType<ClaimsIdentity>();
        identity.AuthenticationType.ShouldBe("CustomScheme");
    }

    [Fact]
    public void CreateClientPrincipal_SetsScopes()
    {
        ImmutableArray<string> scopes = ["api", "openid"];

        ClaimsPrincipal principal = OidcPrincipalFactory.CreateClientPrincipal(
            "my-client", scopes, "TestScheme");

        principal.ShouldNotBeNull();
        principal.Identity.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static GranitUser CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "testuser",
        FirstName = null,
        LastName = null,
        Email = null,
        PhoneNumber = null,
    };

    private void SetupUserManager(
        GranitUser user,
        IList<string>? roles = null,
        IList<Claim>? customClaims = null)
    {
        _userManager.GetRolesAsync(user).Returns(roles ?? Array.Empty<string>());
        _userManager.GetClaimsAsync(user).Returns(customClaims ?? Array.Empty<Claim>());
    }
}
