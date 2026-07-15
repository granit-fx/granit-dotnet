using Granit.Identity;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Options;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
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
    public async Task SeedApplicationAsync_DeviceKindChanged_PerformsUpdate()
    {
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        const string clientId = "showcase-tv";
        object existingApp = new();
        _appManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(existingApp);

        _appManager.When(m => m.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existingApp, Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Showcase TV";
                // Current app declares no device kind — the seed declaring Tv must trigger an update.
            });

        OidcApplicationSeedDescriptor app = new(
            ClientId: clientId,
            ClientSecret: null,
            DisplayName: "Showcase TV",
            Permissions: [],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null,
            DeviceKind: DeviceKind.Tv);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.Received(1).UpdateAsync(
            existingApp,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.GetDeviceKind() == DeviceKind.Tv),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedApplicationAsync_DeviceKindUnknownAndNoneStored_SkipsUpdate()
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
                // No device kind stored.
            });

        // Declaring Unknown is "not declared" — it must normalise to the stored absence, not a phantom diff.
        OidcApplicationSeedDescriptor app = new(
            ClientId: clientId,
            ClientSecret: null,
            DisplayName: "Showcase Web",
            Permissions: [],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            SigningKeyJwk: null,
            DeviceKind: DeviceKind.Unknown);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        await _appManager.DidNotReceive().UpdateAsync(
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

    // -------------------------------------------------------------------------
    // Tenant stamping — TenantId is not an OpenIddict descriptor field
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedApplicationAsync_WithTenantId_StampsTenantOnCreatedEntity()
    {
        var tenant = Guid.Parse("44444444-4444-4444-4444-444444444444");
        GranitOpenIddictApplication created = new() { TenantId = null };

        _appManager.FindByClientIdAsync("tenant-client", Arg.Any<CancellationToken>())
            .Returns((object?)null);
        _appManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(created);

        OidcApplicationSeedDescriptor app = new(
            ClientId: "tenant-client",
            ClientSecret: null,
            DisplayName: "Tenant Client",
            Permissions: [],
            RedirectUris: [],
            PostLogoutRedirectUris: [],
            TenantId: tenant);

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        created.TenantId.ShouldBe(tenant);
        await _appManager.Received(1).UpdateAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedApplicationAsync_GlobalApplication_DoesNotStampOrUpdate()
    {
        GranitOpenIddictApplication created = new() { TenantId = null };

        _appManager.FindByClientIdAsync("global-client", Arg.Any<CancellationToken>())
            .Returns((object?)null);
        _appManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(created);

        OidcApplicationSeedDescriptor app = new(
            ClientId: "global-client",
            ClientSecret: null,
            DisplayName: "Global Client",
            Permissions: [],
            RedirectUris: [],
            PostLogoutRedirectUris: []); // TenantId defaults to null (global)

        OpenIddictSeedContributor contributor = CreateContributor(apps: [app]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        created.TenantId.ShouldBeNull();
        await _appManager.DidNotReceive().UpdateAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedScopeAsync_WithTenantId_StampsTenantOnCreatedEntity()
    {
        var tenant = Guid.Parse("55555555-5555-5555-5555-555555555555");
        GranitOpenIddictScope created = new() { TenantId = null };

        // Every scope (standard + configured) is not found → create path; the configured scope's
        // CreateAsync returns our tenant-owned entity so we can assert the stamp.
        _scopeManager.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);
        _scopeManager.CreateAsync(
                Arg.Is<OpenIddictScopeDescriptor>(d => d.Name == "tenant-scope"),
                Arg.Any<CancellationToken>())
            .Returns(created);

        OidcScopeSeedDescriptor scope = new(
            Name: "tenant-scope",
            DisplayName: "Tenant Scope",
            Resources: [],
            TenantId: tenant);

        OpenIddictSeedContributor contributor = CreateContributor(scopes: [scope]);
        await contributor.SeedAsync(Ctx, TestContext.Current.CancellationToken);

        created.TenantId.ShouldBe(tenant);
        await _scopeManager.Received(1).UpdateAsync(created, Arg.Any<CancellationToken>());
    }
}
