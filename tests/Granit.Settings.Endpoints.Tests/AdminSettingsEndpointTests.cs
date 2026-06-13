using System.Net;
using System.Net.Http.Json;
using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Extensions;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Granit.Testing.Endpoints;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class AdminSettingsEndpointTests : IAsyncDisposable
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingManager _settingManager = Substitute.For<ISettingManager>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _globalReadClient;
    private readonly HttpClient _globalManageClient;
    private readonly HttpClient _tenantReadClient;
    private readonly HttpClient _tenantManageClient;
    private readonly HttpClient _anonClient;

    public AdminSettingsEndpointTests()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(TestAuthHandler.TestUserId);
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TenantId);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(SettingsPermissions.Global.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, SettingsPermissions.Global.Read))
            .AddPolicy(SettingsPermissions.Global.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, SettingsPermissions.Global.Manage))
            .AddPolicy(SettingsPermissions.Tenant.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, SettingsPermissions.Tenant.Read))
            .AddPolicy(SettingsPermissions.Tenant.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, SettingsPermissions.Tenant.Manage));

        builder.Services.AddSingleton(_settingProvider);
        builder.Services.AddSingleton(_settingManager);
        builder.Services.AddSingleton(_currentUserService);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton<ISettingDefinitionProvider, WellKnownSettingDefinitionProvider>();
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionManager(sp.GetServices<ISettingDefinitionProvider>()));

        _app = builder.Build();
        _app.MapGranitGlobalSettings();
        _app.MapGranitTenantSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _globalReadClient = BuildClient(SettingsPermissions.Global.Read);
        _globalManageClient = BuildClient(SettingsPermissions.Global.Manage);
        _tenantReadClient = BuildClient(SettingsPermissions.Tenant.Read);
        _tenantManageClient = BuildClient(SettingsPermissions.Tenant.Manage);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _globalReadClient.Dispose();
        _globalManageClient.Dispose();
        _tenantReadClient.Dispose();
        _tenantManageClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // GET /settings/global
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllGlobal_Authorized_Returns_200()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(WellKnownSettingNames.PreferredCulture, "G", null, "en"),
            ]);

        HttpResponseMessage response = await _globalReadClient.GetAsync(
            "/settings/global", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldContainKey(WellKnownSettingNames.PreferredCulture);
    }

    [Fact]
    public async Task GetAllGlobal_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            "/settings/global", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllGlobal_WrongRole_Returns_403()
    {
        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/global", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // PUT /settings/global/{name}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutGlobal_Authorized_Returns_204()
    {
        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            $"/settings/global/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("en"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _settingManager.Received(1).SetGlobalAsync(
            WellKnownSettingNames.PreferredCulture,
            "en",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutGlobal_Unknown_Setting_Returns_404()
    {
        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/Unknown.Setting",
            new UpdateSettingValueRequest("value"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // GET /settings/tenant
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllTenant_Authorized_Returns_200()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(WellKnownSettingNames.PreferredCulture, "T", TenantId.ToString(), "nl"),
            ]);

        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/tenant", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllTenant_NoTenantContext_Returns_400()
    {
        _currentTenant.IsAvailable.Returns(false);

        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/tenant", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // PUT /settings/tenant/{name}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutTenant_Authorized_Returns_204()
    {
        HttpResponseMessage response = await _tenantManageClient.PutAsJsonAsync(
            $"/settings/tenant/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("nl"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _settingManager.Received(1).SetForTenantAsync(
            TenantId,
            WellKnownSettingNames.PreferredCulture,
            "nl",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutTenant_NoTenantContext_Returns_400()
    {
        _currentTenant.IsAvailable.Returns(false);
        _currentTenant.Id.Returns((Guid?)null);

        HttpResponseMessage response = await _tenantManageClient.PutAsJsonAsync(
            $"/settings/tenant/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("nl"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Authorization matrix
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutGlobal_ReadOnlyRole_Returns_403()
    {
        HttpResponseMessage response = await _globalReadClient.PutAsJsonAsync(
            $"/settings/global/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("en"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutGlobal_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"/settings/global/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("en"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTenant_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            "/settings/tenant", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutTenant_ReadOnlyRole_Returns_403()
    {
        HttpResponseMessage response = await _tenantReadClient.PutAsJsonAsync(
            $"/settings/tenant/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("nl"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutTenant_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"/settings/tenant/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("nl"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private HttpClient BuildClient(string permission)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, permission);
        return client;
    }
}
