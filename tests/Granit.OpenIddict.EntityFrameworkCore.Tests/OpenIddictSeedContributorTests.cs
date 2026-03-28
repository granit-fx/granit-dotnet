using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Options;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

public sealed class OpenIddictSeedContributorTests
{
    private readonly IOpenIddictApplicationManager _appManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOpenIddictScopeManager _scopeManager = Substitute.For<IOpenIddictScopeManager>();
    private static readonly DataSeedContext Ctx = new(null);

    private OpenIddictSeedContributor CreateContributor(
        OidcApplicationSeedDescriptor[]? apps = null,
        OidcScopeSeedDescriptor[]? scopes = null)
    {
        GranitOpenIddictSeedingOptions opts = new()
        {
            Applications = apps ?? [],
            Scopes = scopes ?? [],
        };

        return new OpenIddictSeedContributor(
            _appManager,
            _scopeManager,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<OpenIddictSeedContributor>.Instance);
    }

    /// <summary>
    /// Makes FindByNameAsync return null for every scope name except <paramref name="existingName"/>,
    /// which returns <paramref name="existingScope"/>. Standard scopes take the create path,
    /// isolating the test to the single scope under test.
    /// </summary>
    private void SetupStandardScopesNotFound(string existingName, object existingScope)
    {
        _scopeManager.FindByNameAsync(
                Arg.Is<string>(n => n != existingName),
                Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _scopeManager.FindByNameAsync(existingName, Arg.Any<CancellationToken>())
            .Returns(existingScope);
    }

    // -------------------------------------------------------------------------
    // SeedScopeAsync — skip when already up to date
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedScopeAsync_AlreadyUpToDate_SkipsUpdate()
    {
        const string scopeName = "api";
        object existingScope = new();
        SetupStandardScopesNotFound(scopeName, existingScope);

        _scopeManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictScopeDescriptor>(), existingScope, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.DisplayName = "API";
                d.Resources.Add("my-api");
            });

        OidcScopeSeedDescriptor userScope = new(scopeName, "API", ["my-api"]);
        OpenIddictSeedContributor contributor = CreateContributor(scopes: [userScope]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _scopeManager.DidNotReceive().UpdateAsync(
            existingScope,
            Arg.Any<OpenIddictScopeDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedScopeAsync_DisplayNameChanged_PerformsUpdate()
    {
        const string scopeName = "api";
        object existingScope = new();
        SetupStandardScopesNotFound(scopeName, existingScope);

        _scopeManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictScopeDescriptor>(), existingScope, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.DisplayName = "Old API display name";
                d.Resources.Add("my-api");
            });

        OidcScopeSeedDescriptor userScope = new(scopeName, "New API display name", ["my-api"]);
        OpenIddictSeedContributor contributor = CreateContributor(scopes: [userScope]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _scopeManager.Received(1).UpdateAsync(
            existingScope,
            Arg.Any<OpenIddictScopeDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedScopeAsync_ResourcesChanged_PerformsUpdate()
    {
        const string scopeName = "api";
        object existingScope = new();
        SetupStandardScopesNotFound(scopeName, existingScope);

        _scopeManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictScopeDescriptor>(), existingScope, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.DisplayName = "API";
                d.Resources.Add("old-resource");
            });

        OidcScopeSeedDescriptor userScope = new(scopeName, "API", ["new-resource"]);
        OpenIddictSeedContributor contributor = CreateContributor(scopes: [userScope]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _scopeManager.Received(1).UpdateAsync(
            existingScope,
            Arg.Is<OpenIddictScopeDescriptor>(d => d.Resources.Contains("new-resource")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedScopeAsync_ScopeNotFound_CreatesNew()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        OidcScopeSeedDescriptor userScope = new("custom", "Custom scope", []);
        OpenIddictSeedContributor contributor = CreateContributor(scopes: [userScope]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        // 7 standard + 1 user scope = 8 creates, 0 updates
        await _scopeManager.Received(8).CreateAsync(
            Arg.Any<OpenIddictScopeDescriptor>(), Arg.Any<CancellationToken>());

        await _scopeManager.DidNotReceive().UpdateAsync(
            Arg.Any<object>(), Arg.Any<OpenIddictScopeDescriptor>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SeedApplicationAsync — skip when already up to date
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedApplicationAsync_AlreadyUpToDate_SkipsUpdate()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        const string clientId = "showcase-web";
        object existingApp = new();
        _appManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(existingApp);

        _appManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existingApp, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Showcase Web";
                d.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                d.RedirectUris.Add(new Uri("https://app.example.com/callback"));
            });

        OidcApplicationSeedDescriptor app = new(
            ClientId: clientId,
            ClientSecret: "secret",
            DisplayName: "Showcase Web",
            Permissions: [OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode],
            RedirectUris: ["https://app.example.com/callback"],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.DidNotReceive().UpdateAsync(
            existingApp,
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedApplicationAsync_PermissionsChanged_PerformsUpdate()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        const string clientId = "showcase-web";
        object existingApp = new();
        _appManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(existingApp);

        _appManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existingApp, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Showcase Web";
                // Current permissions are empty — new permission in seed config must trigger update
            });

        OidcApplicationSeedDescriptor app = new(
            ClientId: clientId,
            ClientSecret: "secret",
            DisplayName: "Showcase Web",
            Permissions: [OpenIddictConstants.Permissions.GrantTypes.ClientCredentials],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.Received(1).UpdateAsync(
            existingApp,
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedApplicationAsync_RedirectUriChanged_PerformsUpdate()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        const string clientId = "showcase-web";
        object existingApp = new();
        _appManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(existingApp);

        _appManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existingApp, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Showcase Web";
                d.RedirectUris.Add(new Uri("https://old.example.com/callback"));
            });

        OidcApplicationSeedDescriptor app = new(
            ClientId: clientId,
            ClientSecret: "secret",
            DisplayName: "Showcase Web",
            Permissions: [],
            RedirectUris: ["https://new.example.com/callback"],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.Received(1).UpdateAsync(
            existingApp,
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedApplicationAsync_AppNotFound_CreatesNew()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);
        _appManager.FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        OidcApplicationSeedDescriptor app = new(
            ClientId: "new-client",
            ClientSecret: null,
            DisplayName: "New Client",
            Permissions: [],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientId == "new-client"),
            Arg.Any<CancellationToken>());

        await _appManager.DidNotReceive().UpdateAsync(
            Arg.Any<object>(),
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }
}
