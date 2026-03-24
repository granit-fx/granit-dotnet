using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Authorization.Abstractions;
using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Extensions;
using Granit.Authorization.Endpoints.Permissions;
using Granit.Localization;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

/// <summary>
/// Integration tests for all authorization management endpoints.
/// Uses a TestServer with NSubstitute mocks for authorization services.
/// A custom TestAuthHandler resolves authentication from X-Test-Roles header.
/// </summary>
public sealed class AuthorizationEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/auth";

    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly IPermissionDefinitionManager _definitionManager = Substitute.For<IPermissionDefinitionManager>();
    private readonly IPermissionManagerReader _permissionManagerReader = Substitute.For<IPermissionManagerReader>();
    private readonly IPermissionManagerWriter _permissionManagerWriter = Substitute.For<IPermissionManagerWriter>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;

    // Admin client: has both admin permissions.
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

        // Set up the dynamic policy provider to resolve permission policies.
        // Since we're testing without GranitAuthorizationModule, we register permission
        // policies manually so RequireAuthorization("Permission.Name") works.
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationEndpointsPermissions.Definitions.Read,
                policy => policy.RequireRole("admin"))
            .AddPolicy(AuthorizationEndpointsPermissions.Grants.Manage,
                policy => policy.RequireRole("admin"));

        builder.Services.AddSingleton(_permissionChecker);
        builder.Services.AddSingleton(_definitionManager);
        builder.Services.AddSingleton(_permissionManagerReader);
        builder.Services.AddSingleton(_permissionManagerWriter);
        builder.Services.AddSingleton(_currentTenant);

        _app = builder.Build();
        _app.MapAuthorizationEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient("admin");
        _userClient = BuildClient("regular-user");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET /me ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_WithAuthenticatedUser_Returns200WithGrantedPermissions()
    {
        // Arrange — grant two out of three permissions.
        _permissionChecker.IsGrantedAsync("Invoices.Read", Arg.Any<CancellationToken>())
            .Returns(true);
        _permissionChecker.IsGrantedAsync("Invoices.Create", Arg.Any<CancellationToken>())
            .Returns(true);
        _permissionChecker.IsGrantedAsync("Invoices.Delete", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/me", TestContext.Current.CancellationToken);

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
            $"{Prefix}/me", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WhenNoPermissionsGranted_Returns200WithEmptyList()
    {
        // Arrange — all permissions denied (default).

        // Act
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/me", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MyPermissionsResponse? result =
            await response.Content.ReadFromJsonAsync<MyPermissionsResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Permissions.ShouldBeEmpty();
    }

    // ── GET /definitions ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_WithAdminRole_Returns200WithGroups()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);

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
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDefinitions_WithWrongRole_Returns403()
    {
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── GET /roles/{roleName} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRolePermissions_WithAdminRole_Returns200()
    {
        // Arrange
        _permissionManagerReader.GetGrantedPermissionsAsync("editor", null, Arg.Any<CancellationToken>())
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

        // Act
        HttpResponseMessage response = await _adminClient.PutAsync(
            $"{Prefix}/roles/editor/Invoices.Read", content: null,
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _permissionManagerWriter.Received(1).SetAsync(
            "Invoices.Read", "editor", null, true, Arg.Any<CancellationToken>());
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

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/roles/editor/Invoices.Read",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _permissionManagerWriter.Received(1).SetAsync(
            "Invoices.Read", "editor", null, false, Arg.Any<CancellationToken>());
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

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

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

    // ── Fake authentication handler ────────────────────────────────────────────

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            Claim[] claims =
            [
                new(ClaimTypes.Name, "test-user"),
                .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
