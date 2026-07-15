using System.Net;
using System.Net.Http.Json;
using Granit.Identity;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.Extensions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Integration;

public sealed class AdminOidcEndpointsIntegrationTests : IAsyncLifetime
{
    private OidcEndpointsTestServer _server = null!;

    public async ValueTask InitializeAsync() =>
        _server = await OidcEndpointsTestServer.CreateAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() =>
        await _server.DisposeAsync().ConfigureAwait(false);

    // -------------------------------------------------------------------------
    // Application endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListApplications_ReturnsEmptyList()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/applications", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcApplicationResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcApplicationResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListApplications_ReturnsApplications()
    {
        GranitOpenIddictApplication app1 = new() { TenantId = null };
        GranitOpenIddictApplication app2 = new() { TenantId = Guid.NewGuid() };

        _server.ApplicationManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(app1, app2));

#pragma warning disable CA2012 // NSubstitute mock setup intentionally doesn't await ValueTask
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app1, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "client-1";
                d.DisplayName = "App One";
                d.ApplicationType = "web";
                return new ValueTask();
            });

        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app2, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "client-2";
                d.DisplayName = "App Two";
                d.ApplicationType = "native";
                return new ValueTask();
            });
#pragma warning restore CA2012

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/applications", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcApplicationResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcApplicationResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].ClientId.ShouldBe("client-1");
        result[0].DisplayName.ShouldBe("App One");
        result[0].Type.ShouldBe("web");
        result[0].TenantId.ShouldBeNull();

        result[1].ClientId.ShouldBe("client-2");
        result[1].DisplayName.ShouldBe("App Two");
        result[1].Type.ShouldBe("native");
        result[1].TenantId.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListApplications_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient
            .GetAsync("/admin/oidc/applications", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateApplication_ReturnsCreated()
    {
        GranitOpenIddictApplication createdApp = new() { TenantId = null };

        _server.ApplicationManager.CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>())
            .Returns(createdApp);
#pragma warning disable CA2012 // NSubstitute mock setup intentionally doesn't await ValueTask
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), createdApp, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "new-client";
                d.DisplayName = "New App";
                d.ApplicationType = "web";
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcCreateApplicationRequest request = new("new-client", "New App", null, "web");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.ToString().ShouldBe("/admin/oidc/applications/new-client");

        AdminOidcApplicationResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcApplicationResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("new-client");
        result.DisplayName.ShouldBe("New App");
        result.Type.ShouldBe("web");
        result.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateApplication_DeclaredDeviceKind_PersistsAndReturnsIt()
    {
        GranitOpenIddictApplication createdApp = new() { TenantId = null };

        _server.ApplicationManager.CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>())
            .Returns(createdApp);
#pragma warning disable CA2012 // NSubstitute mock setup intentionally doesn't await ValueTask
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), createdApp, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "tv-client";
                d.DisplayName = "TV App";
                d.ApplicationType = "web";
                d.SetDeviceKind(DeviceKind.Tv);
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcCreateApplicationRequest request = new("tv-client", "TV App", null, "web", DeviceKind: DeviceKind.Tv);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // The endpoint wrote the declared kind onto the descriptor it created.
        await _server.ApplicationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.GetDeviceKind() == DeviceKind.Tv),
            Arg.Any<CancellationToken>());

        AdminOidcApplicationResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcApplicationResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.DeviceKind.ShouldBe(DeviceKind.Tv);
    }

    [Fact]
    public async Task CreateApplication_WithSecret_SetsConfidentialType()
    {
        GranitOpenIddictApplication createdApp = new() { TenantId = null };

        _server.ApplicationManager.CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>())
            .Returns(createdApp);
        _server.ApplicationManager.GetClientIdAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns("confidential-client");
        _server.ApplicationManager.GetDisplayNameAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns("Confidential App");
        _server.ApplicationManager.GetApplicationTypeAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns("web");

        AdminOidcCreateApplicationRequest request = new("confidential-client", "Confidential App", "my-secret", "web");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        await _server.ApplicationManager.Received(1)
            .CreateAsync(
                Arg.Is<OpenIddictApplicationDescriptor>(d =>
                    d.ClientSecret == "my-secret" &&
                    d.ClientType == OpenIddictConstants.ClientTypes.Confidential),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApplication_WithoutSecret_SetsPublicType()
    {
        GranitOpenIddictApplication createdApp = new() { TenantId = null };

        _server.ApplicationManager.CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>())
            .Returns(createdApp);
        _server.ApplicationManager.GetClientIdAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns("public-client");
        _server.ApplicationManager.GetDisplayNameAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _server.ApplicationManager.GetApplicationTypeAsync(createdApp, Arg.Any<CancellationToken>())
            .Returns("web");

        AdminOidcCreateApplicationRequest request = new("public-client", null, null, null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        await _server.ApplicationManager.Received(1)
            .CreateAsync(
                Arg.Is<OpenIddictApplicationDescriptor>(d =>
                    d.ClientSecret == null &&
                    d.ClientType == OpenIddictConstants.ClientTypes.Public),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApplication_EmptyClientId_ReturnsValidationError()
    {
        AdminOidcCreateApplicationRequest request = new("", "Name", null, null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateApplication_Anonymous_Returns401()
    {
        AdminOidcCreateApplicationRequest request = new("client", "Name", null, null);

        HttpResponseMessage response = await _server.AnonymousClient
            .PostAsJsonAsync("/admin/oidc/applications", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteApplication_ExistingApp_Returns204()
    {
        object existingApp = new GranitOpenIddictApplication();

        _server.ApplicationManager.FindByClientIdAsync("client-to-delete", Arg.Any<CancellationToken>())
            .Returns(existingApp);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync("/admin/oidc/applications/client-to-delete", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.ApplicationManager.Received(1)
            .DeleteAsync(existingApp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteApplication_NotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync("/admin/oidc/applications/nonexistent", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RotateSecret_ExistingApp_ReturnsNewSecret()
    {
        GranitOpenIddictApplication existingApp = new();

        _server.ApplicationManager.FindByClientIdAsync("rotate-client", Arg.Any<CancellationToken>())
            .Returns(existingApp);
        _server.ApplicationManager.GetDisplayNameAsync(existingApp, Arg.Any<CancellationToken>())
            .Returns("Rotate App");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsync("/admin/oidc/applications/rotate-client/rotate-secret", null,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AdminOidcRotateSecretResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcRotateSecretResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("rotate-client");
        result.DisplayName.ShouldBe("Rotate App");
        result.NewClientSecret.ShouldNotBeNullOrWhiteSpace();

        // Verify the manager was called with confidential type and a secret
        await _server.ApplicationManager.Received(1)
            .UpdateAsync(
                existingApp,
                Arg.Is<OpenIddictApplicationDescriptor>(d =>
                    d.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
                    !string.IsNullOrEmpty(d.ClientSecret)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateSecret_NotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsync("/admin/oidc/applications/nonexistent/rotate-secret", null,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // Scope endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListScopes_ReturnsEmptyList()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/scopes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcScopeResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcScopeResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListScopes_ReturnsScopes()
    {
        object scope1 = new();
        object scope2 = new();

        _server.ScopeManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(scope1, scope2));

#pragma warning disable CA2012
        _server.ScopeManager
            .PopulateAsync(Arg.Any<OpenIddictScopeDescriptor>(), scope1, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.Name = "openid"; d.DisplayName = "OpenID"; d.Description = "OpenID Connect scope";
                return new ValueTask();
            });

        _server.ScopeManager
            .PopulateAsync(Arg.Any<OpenIddictScopeDescriptor>(), scope2, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.Name = "profile"; d.DisplayName = "Profile"; d.Description = "User profile scope";
                d.Resources.Add("api://granit");
                return new ValueTask();
            });
#pragma warning restore CA2012

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/scopes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcScopeResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcScopeResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].Name.ShouldBe("openid");
        result[0].DisplayName.ShouldBe("OpenID");
        result[0].Description.ShouldBe("OpenID Connect scope");
        result[0].Resources.ShouldBeEmpty();

        result[1].Name.ShouldBe("profile");
        result[1].DisplayName.ShouldBe("Profile");
        result[1].Description.ShouldBe("User profile scope");
        result[1].Resources.ShouldBe(["api://granit"]);
    }

    [Fact]
    public async Task ListScopes_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient
            .GetAsync("/admin/oidc/scopes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateScope_ReturnsCreated()
    {
        object createdScope = new();

        _server.ScopeManager.CreateAsync(
            Arg.Any<OpenIddictScopeDescriptor>(),
            Arg.Any<CancellationToken>())
            .Returns(createdScope);

#pragma warning disable CA2012
        _server.ScopeManager
            .PopulateAsync(Arg.Any<OpenIddictScopeDescriptor>(), createdScope, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.Name = "custom_scope"; d.DisplayName = "Custom Scope";
                d.Description = "A custom OIDC scope";
                d.Resources.Add("api://my-resource");
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcCreateScopeRequest request = new(
            "custom_scope", "Custom Scope", "A custom OIDC scope",
            Resources: ["api://my-resource"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/scopes", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.ToString().ShouldBe("/admin/oidc/scopes/custom_scope");

        AdminOidcScopeResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcScopeResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("custom_scope");
        result.DisplayName.ShouldBe("Custom Scope");
        result.Description.ShouldBe("A custom OIDC scope");
        result.Resources.ShouldBe(["api://my-resource"]);
    }

    [Fact]
    public async Task CreateScope_EmptyName_ReturnsValidationError()
    {
        AdminOidcCreateScopeRequest request = new("", "Display", null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/scopes", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteScope_ExistingScope_Returns204()
    {
        object existingScope = new();

        _server.ScopeManager.FindByNameAsync("scope-to-delete", Arg.Any<CancellationToken>())
            .Returns(existingScope);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync("/admin/oidc/scopes/scope-to-delete", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.ScopeManager.Received(1)
            .DeleteAsync(existingScope, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteScope_NotFound_Returns404()
    {
        _server.ScopeManager.FindByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync("/admin/oidc/scopes/nonexistent", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // Authorization endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListAuthorizations_ReturnsEmptyList()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/authorizations", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcAuthorizationResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcAuthorizationResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAuthorizations_ReturnsAuthorizations()
    {
        var authId = Guid.NewGuid();
        object auth1 = new();
        object app1 = new();

        _server.AuthorizationManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(auth1));

        _server.AuthorizationManager.GetIdAsync(auth1, Arg.Any<CancellationToken>())
            .Returns(authId.ToString());

#pragma warning disable CA2012
        _server.AuthorizationManager
            .PopulateAsync(Arg.Any<OpenIddictAuthorizationDescriptor>(), auth1, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictAuthorizationDescriptor d = ci.ArgAt<OpenIddictAuthorizationDescriptor>(0);
                d.Subject = "user-123";
                d.Status = "valid";
                d.Type = "permanent";
                d.ApplicationId = "app-internal-id";
                d.Scopes.Add("openid");
                d.Scopes.Add("profile");
                return new ValueTask();
            });
#pragma warning restore CA2012

        _server.ApplicationManager.FindByIdAsync("app-internal-id", Arg.Any<CancellationToken>())
            .Returns(app1);
        _server.ApplicationManager.GetClientIdAsync(app1, Arg.Any<CancellationToken>())
            .Returns("my-client");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/authorizations", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminOidcAuthorizationResponse>? result = await response.Content
            .ReadFromJsonAsync<List<AdminOidcAuthorizationResponse>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(authId);
        result[0].Subject.ShouldBe("user-123");
        result[0].ClientId.ShouldBe("my-client");
        result[0].Status.ShouldBe("valid");
        result[0].Type.ShouldBe("permanent");
        result[0].Scopes.ShouldBe(["openid", "profile"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListAuthorizations_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient
            .GetAsync("/admin/oidc/authorizations", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeAuthorization_ExistingAuth_Returns204()
    {
        var authId = Guid.NewGuid();
        object existingAuth = new();
        object token1 = new();

        _server.AuthorizationManager.FindByIdAsync(authId.ToString(), Arg.Any<CancellationToken>())
            .Returns(existingAuth);

        _server.TokenManager.FindByAuthorizationIdAsync(authId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(token1));

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync($"/admin/oidc/authorizations/{authId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.TokenManager.Received(1)
            .TryRevokeAsync(token1, Arg.Any<CancellationToken>());

        await _server.AuthorizationManager.Received(1)
            .DeleteAsync(existingAuth, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAuthorization_NotFound_Returns404()
    {
        var authId = Guid.NewGuid();

        _server.AuthorizationManager.FindByIdAsync(authId.ToString(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync($"/admin/oidc/authorizations/{authId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeUserAuthorizations_RevokesAllTokensAndAuthorizations()
    {
        var userId = Guid.NewGuid();
        string subject = userId.ToString();

        object token1 = new();
        object token2 = new();
        object auth1 = new();

        _server.TokenManager.FindBySubjectAsync(subject, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(token1, token2));

        _server.AuthorizationManager.FindBySubjectAsync(subject, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>(auth1));

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync($"/admin/oidc/authorizations/user/{userId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.TokenManager.Received(1)
            .TryRevokeAsync(token1, Arg.Any<CancellationToken>());
        await _server.TokenManager.Received(1)
            .TryRevokeAsync(token2, Arg.Any<CancellationToken>());
        await _server.AuthorizationManager.Received(1)
            .DeleteAsync(auth1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeUserAuthorizations_NoTokensOrAuths_Returns204()
    {
        var userId = Guid.NewGuid();
        string subject = userId.ToString();

        _server.TokenManager.FindBySubjectAsync(subject, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<object>());

        _server.AuthorizationManager.FindBySubjectAsync(subject, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<object>());

        HttpResponseMessage response = await _server.AuthenticatedClient
            .DeleteAsync($"/admin/oidc/authorizations/user/{userId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // GetApplication endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetApplication_ExistingApp_ReturnsApp()
    {
        GranitOpenIddictApplication app = new() { TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") };

        _server.ApplicationManager.FindByClientIdAsync("my-client", Arg.Any<CancellationToken>())
            .Returns(app);

#pragma warning disable CA2012
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "my-client";
                d.DisplayName = "My App";
                d.ApplicationType = "web";
                d.ConsentType = OpenIddictConstants.ConsentTypes.Implicit;
                d.Permissions.Add("ept:token");
                d.RedirectUris.Add(new Uri("https://app.example.com/callback"));
                return new ValueTask();
            });
#pragma warning restore CA2012

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/applications/my-client", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AdminOidcApplicationResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcApplicationResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("my-client");
        result.DisplayName.ShouldBe("My App");
        result.Type.ShouldBe("web");
        result.TenantId.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Permissions.ShouldContain("ept:token");
        result.RedirectUris.ShouldContain("https://app.example.com/callback");
    }

    [Fact]
    public async Task GetApplication_NotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/admin/oidc/applications/nonexistent", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetApplication_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient
            .GetAsync("/admin/oidc/applications/my-client", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // UpdateApplication endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateApplication_ExistingApp_ReturnsUpdatedApp()
    {
        GranitOpenIddictApplication app = new() { TenantId = null };

        _server.ApplicationManager.FindByClientIdAsync("update-client", Arg.Any<CancellationToken>())
            .Returns(app);

#pragma warning disable CA2012
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "update-client";
                d.DisplayName = "Updated Name";
                d.ApplicationType = "web";
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcUpdateApplicationRequest request = new(DisplayName: "Updated Name");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/applications/update-client", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AdminOidcApplicationResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcApplicationResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("update-client");
        result.DisplayName.ShouldBe("Updated Name");

        await _server.ApplicationManager.Received(1)
            .UpdateAsync(app, Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateApplication_ReplacesPermissionsWhenProvided()
    {
        GranitOpenIddictApplication app = new() { TenantId = null };

        _server.ApplicationManager.FindByClientIdAsync("perm-client", Arg.Any<CancellationToken>())
            .Returns(app);

#pragma warning disable CA2012
        _server.ApplicationManager
            .PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.ClientId = "perm-client";
                d.Permissions.Add("ept:token");
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcUpdateApplicationRequest request = new(Permissions: ["ept:token", "gt:client_credentials"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/applications/perm-client", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await _server.ApplicationManager.Received(1)
            .UpdateAsync(
                app,
                Arg.Is<OpenIddictApplicationDescriptor>(d =>
                    d.Permissions.Contains("ept:token") &&
                    d.Permissions.Contains("gt:client_credentials")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateApplication_NotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        AdminOidcUpdateApplicationRequest request = new(DisplayName: "New Name");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/applications/nonexistent", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateApplication_Anonymous_Returns401()
    {
        AdminOidcUpdateApplicationRequest request = new(DisplayName: "New Name");

        HttpResponseMessage response = await _server.AnonymousClient
            .PutAsJsonAsync("/admin/oidc/applications/my-client", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateApplication_InvalidRedirectUri_ReturnsValidationError()
    {
        AdminOidcUpdateApplicationRequest request = new(RedirectUris: ["not-a-valid-uri"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/applications/any-client", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // UpdateScope endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateScope_ExistingScope_ReturnsUpdatedScope()
    {
        object scope = new();

        _server.ScopeManager.FindByNameAsync("openid", Arg.Any<CancellationToken>())
            .Returns(scope);

#pragma warning disable CA2012
        _server.ScopeManager
            .PopulateAsync(Arg.Any<OpenIddictScopeDescriptor>(), scope, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.Name = "openid";
                d.DisplayName = "OpenID Connect";
                d.Description = "Updated description";
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcUpdateScopeRequest request = new(Description: "Updated description");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/scopes/openid", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AdminOidcScopeResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcScopeResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("openid");
        result.Description.ShouldBe("Updated description");

        await _server.ScopeManager.Received(1)
            .UpdateAsync(scope, Arg.Any<OpenIddictScopeDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateScope_ClearsResourcesWhenEmptyArrayPassed()
    {
        object scope = new();

        _server.ScopeManager.FindByNameAsync("profile", Arg.Any<CancellationToken>())
            .Returns(scope);

#pragma warning disable CA2012
        _server.ScopeManager
            .PopulateAsync(Arg.Any<OpenIddictScopeDescriptor>(), scope, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictScopeDescriptor d = ci.ArgAt<OpenIddictScopeDescriptor>(0);
                d.Name = "profile";
                return new ValueTask();
            });
#pragma warning restore CA2012

        AdminOidcUpdateScopeRequest request = new(Resources: []);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/scopes/profile", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await _server.ScopeManager.Received(1)
            .UpdateAsync(
                scope,
                Arg.Is<OpenIddictScopeDescriptor>(d => d.Resources.Count == 0),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateScope_NotFound_Returns404()
    {
        _server.ScopeManager.FindByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        AdminOidcUpdateScopeRequest request = new(DisplayName: "New Name");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PutAsJsonAsync("/admin/oidc/scopes/nonexistent", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateScope_Anonymous_Returns401()
    {
        AdminOidcUpdateScopeRequest request = new(DisplayName: "New Name");

        HttpResponseMessage response = await _server.AnonymousClient
            .PutAsJsonAsync("/admin/oidc/scopes/openid", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // CreateAuthorization endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAuthorization_ReturnsCreated()
    {
        object app = new();
        object createdAuth = new();
        var authId = Guid.NewGuid();

        _server.ApplicationManager.FindByClientIdAsync("my-client", Arg.Any<CancellationToken>())
            .Returns(app);
        _server.ApplicationManager.GetIdAsync(app, Arg.Any<CancellationToken>())
            .Returns("app-internal-id");

        _server.AuthorizationManager
            .CreateAsync(Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(createdAuth);
        _server.AuthorizationManager.GetIdAsync(createdAuth, Arg.Any<CancellationToken>())
            .Returns(authId.ToString());

        AdminOidcCreateAuthorizationRequest request = new(
            Subject: "user-abc",
            ClientId: "my-client",
            Scopes: ["openid", "profile"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/authorizations", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        AdminOidcAuthorizationResponse? result = await response.Content
            .ReadFromJsonAsync<AdminOidcAuthorizationResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(authId);
        result.Subject.ShouldBe("user-abc");
        result.ClientId.ShouldBe("my-client");
        result.Status.ShouldBe(OpenIddictConstants.Statuses.Valid);
        result.Type.ShouldBe(OpenIddictConstants.AuthorizationTypes.Permanent);
        result.Scopes.ShouldBe(["openid", "profile"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateAuthorization_ClientNotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        AdminOidcCreateAuthorizationRequest request = new(
            Subject: "user-abc",
            ClientId: "unknown-client",
            Scopes: ["openid"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/authorizations", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAuthorization_EmptySubject_ReturnsValidationError()
    {
        AdminOidcCreateAuthorizationRequest request = new(
            Subject: "",
            ClientId: "my-client",
            Scopes: ["openid"]);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .PostAsJsonAsync("/admin/oidc/authorizations", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateAuthorization_Anonymous_Returns401()
    {
        AdminOidcCreateAuthorizationRequest request = new(
            Subject: "user-abc",
            ClientId: "my-client",
            Scopes: ["openid"]);

        HttpResponseMessage response = await _server.AnonymousClient
            .PostAsJsonAsync("/admin/oidc/authorizations", request,
                TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // ConsentOidcEndpoints — GET /oidc/applications/{clientId}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetApplicationInfo_ExistingApp_ReturnsClientIdAndDisplayName()
    {
        object app = new();

        _server.ApplicationManager.FindByClientIdAsync("consent-client", Arg.Any<CancellationToken>())
            .Returns(app);
        _server.ApplicationManager.GetDisplayNameAsync(app, Arg.Any<CancellationToken>())
            .Returns("Consent App");

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/oidc/applications/consent-client", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        OidcApplicationInfoResponse? result = await response.Content
            .ReadFromJsonAsync<OidcApplicationInfoResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe("consent-client");
        result.DisplayName.ShouldBe("Consent App");
    }

    [Fact]
    public async Task GetApplicationInfo_NotFound_Returns404()
    {
        _server.ApplicationManager.FindByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        HttpResponseMessage response = await _server.AuthenticatedClient
            .GetAsync("/oidc/applications/unknown-client", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
