using System.Net;
using System.Net.Http.Json;
using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Extensions;
using Granit.Authorization.Endpoints.Permissions;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

/// <summary>
/// Integration tests for all authorization management endpoints.
/// Uses <see cref="GranitEndpointTestHost"/> with NSubstitute mocks for authorization services.
/// Authorization is permission-based: the shared <see cref="TestAuthHandler"/> resolves the caller's
/// granted permissions from the X-Test-Permissions header, and the manually registered policies gate on
/// the matching permission claim.
/// </summary>
public sealed class AuthorizationEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/authorization";

    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly IPermissionDefinitionRegistry _definitionManager = Substitute.For<IPermissionDefinitionRegistry>();
    private readonly IPermissionManagerReader _permissionManagerReader = Substitute.For<IPermissionManagerReader>();
    private readonly IPermissionManagerWriter _permissionManagerWriter = Substitute.For<IPermissionManagerWriter>();
    private readonly IRoleMetadataStore _roleMetadataStore = Substitute.For<IRoleMetadataStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly GranitEndpointTestHost _host;

    // Admin client: carries both admin permissions.
    private readonly HttpClient _adminClient;

    // Authenticated user without admin permissions.
    private readonly HttpClient _userClient;

    // Unauthenticated client.
    private readonly HttpClient _anonClient;

    public AuthorizationEndpointsTests()
    {
        _currentTenant.IsAvailable.Returns(false);

        // Set up the permission checker to grant admin permissions to admin role.
        _permissionChecker
            .IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // This will be configured per-test. Default: deny all.
                return Task.FromResult(false);
            });

        // Set up the definition manager to return a default permission set.
        SetupDefaultDefinitions();

        // We test without GranitAuthorizationModule, so register the permission policies manually
        // so RequireAuthorization("Permission.Name") works. Each policy is satisfied by the matching
        // permission claim emitted by the shared TestAuthHandler from the X-Test-Permissions header.
        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(AuthorizationEndpointsPermissions.Definitions.Read,
                        policy => policy.RequireClaim(
                            TestAuthHandler.PermissionClaimType,
                            AuthorizationEndpointsPermissions.Definitions.Read))
                    .AddPolicy(AuthorizationEndpointsPermissions.Grants.Manage,
                        policy => policy.RequireClaim(
                            TestAuthHandler.PermissionClaimType,
                            AuthorizationEndpointsPermissions.Grants.Manage));

                services.AddSingleton(_permissionChecker);
                services.AddSingleton(_definitionManager);
                services.AddSingleton(_permissionManagerReader);
                services.AddSingleton(_permissionManagerWriter);
                services.AddSingleton(_roleMetadataStore);
                services.AddSingleton(_currentTenant);
            },
            configureEndpoints: app => app.MapGranitAuthorization())
            .GetAwaiter().GetResult();

        // Admin carries both gating permissions; the regular user is authenticated and holds an
        // unrelated permission (never the gating ones), so the admin endpoints answer 403, not 401.
        _adminClient = _host.CreateClientWithPermissions(
            AuthorizationEndpointsPermissions.Definitions.Read,
            AuthorizationEndpointsPermissions.Grants.Manage);
        _userClient = _host.CreateClientWithPermissions("Invoices.Read");
        _anonClient = _host.CreateAnonymousClient();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    // ── GET /permissions ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_WithAuthenticatedUser_Returns200WithGrantedPermissions()
    {
        // Arrange — grant two out of three permissions via batch method.
        _permissionChecker.GetGrantedAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                IReadOnlyList<string> requested = callInfo.Arg<IReadOnlyList<string>>();
                HashSet<string> granted = ["Invoices.Read", "Invoices.Create"];
                return Task.FromResult<IReadOnlyList<string>>(
                    requested.Where(granted.Contains).ToList());
            });

        // Act
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/permissions", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MyPermissionsResponse? result =
            await response.Content.ReadFromJsonAsync<MyPermissionsResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Permissions.ShouldContain("Invoices.Read");
        result.Permissions.ShouldContain("Invoices.Create");
        result.Permissions.ShouldNotContain("Invoices.Delete");
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/permissions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WhenNoPermissionsGranted_Returns200WithEmptyList()
    {
        // Arrange — all permissions denied.
        _permissionChecker.GetGrantedAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));

        // Act
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/permissions", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MyPermissionsResponse? result =
            await response.Content.ReadFromJsonAsync<MyPermissionsResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Permissions.ShouldBeEmpty();
    }

    // ── GET /permissions/definitions ────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_WithAdminRole_Returns200WithGroups()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/permissions/definitions", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<PermissionGroupResponse>? result =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<PermissionGroupResponse>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Invoices");
        result[0].Permissions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetDefinitions_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/permissions/definitions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDefinitions_WithWrongRole_Returns403()
    {
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/permissions/definitions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── GET /roles/{roleName} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRolePermissions_WithAdminRole_Returns200()
    {
        // Arrange
        _permissionManagerReader.GetGrantedPermissionsAsync("R", "editor", null, Arg.Any<CancellationToken>())
            .Returns(["Invoices.Read", "Invoices.Create"]);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/roles/editor", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PermissionGrantResponse? result =
            await response.Content.ReadFromJsonAsync<PermissionGrantResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.RoleName.ShouldBe("editor");
        result.Permissions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetRolePermissions_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/roles/editor", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRolePermissions_WithWrongRole_Returns403()
    {
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/roles/editor", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── PUT /roles/{roleName}/{permissionName} ─────────────────────────────────

    [Fact]
    public async Task GrantPermission_WithAdminRole_Returns204()
    {
        // Arrange
        _definitionManager.Exists("Invoices.Read").Returns(true);
        _permissionChecker.IsGrantedAsync("Invoices.Read", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsync(
            $"{Prefix}/roles/editor/Invoices.Read", content: null,
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _permissionManagerWriter.Received(1).SetAsync(
            "Invoices.Read", "R", "editor", null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantPermission_WhenPermissionNotDefined_Returns400()
    {
        // Arrange
        _definitionManager.Exists("Unknown.Permission").Returns(false);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsync(
            $"{Prefix}/roles/editor/Unknown.Permission", content: null,
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GrantPermission_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PutAsync(
            $"{Prefix}/roles/editor/Invoices.Read", content: null,
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GrantPermission_WithWrongRole_Returns403()
    {
        HttpResponseMessage response = await _userClient.PutAsync(
            $"{Prefix}/roles/editor/Invoices.Read", content: null,
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── DELETE /roles/{roleName}/{permissionName} ──────────────────────────────

    [Fact]
    public async Task RevokePermission_WithAdminRole_Returns204()
    {
        // Arrange
        _definitionManager.Exists("Invoices.Read").Returns(true);
        _permissionChecker.IsGrantedAsync("Invoices.Read", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/roles/editor/Invoices.Read",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _permissionManagerWriter.Received(1).SetAsync(
            "Invoices.Read", "R", "editor", null, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokePermission_WhenPermissionNotDefined_Returns400()
    {
        // Arrange
        _definitionManager.Exists("Unknown.Permission").Returns(false);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/roles/editor/Unknown.Permission",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RevokePermission_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/roles/editor/Invoices.Read",
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetupDefaultDefinitions()
    {
        PermissionGroup invoicesGroup = new("Invoices", LocalizableString.Fixed("Invoice Management"));
        invoicesGroup.AddPermission("Invoices.Read", LocalizableString.Fixed("Read invoices"));
        invoicesGroup.AddPermission("Invoices.Create", LocalizableString.Fixed("Create invoices"));
        invoicesGroup.AddPermission("Invoices.Delete", LocalizableString.Fixed("Delete invoices"));

        _definitionManager.GetGroups().Returns([invoicesGroup]);
        _definitionManager.GetAll().Returns(invoicesGroup.Permissions);
        _definitionManager.Exists(Arg.Any<string>()).Returns(callInfo =>
        {
            string name = callInfo.Arg<string>();
            return invoicesGroup.Permissions.Any(p => p.Name == name);
        });
    }
}
