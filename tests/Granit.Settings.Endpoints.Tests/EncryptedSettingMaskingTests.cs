using System.Net;
using System.Net.Http.Json;
using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Extensions;
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

/// <summary>
/// Verifies that settings marked as <see cref="SettingDefinition.IsEncrypted"/>
/// have their values masked with "***" in all read endpoints, while non-encrypted
/// settings remain in plaintext.
/// </summary>
public sealed class EncryptedSettingMaskingTests : IAsyncDisposable
{
    private const string EncryptedSettingName = "Test.EncryptedApiKey";
    private const string PlainSettingName = "Test.PlainSetting";
    private const string MaskedValue = "***";

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    // The user-scoped read endpoint uses bare RequireAuthorization() — no permission
    // gates it. The caller only needs to be authenticated; this non-empty marker makes
    // the permissions header present (an empty header is dropped in transit) without
    // granting any Settings permission.
    private const string AuthenticatedMarker = "authenticated";

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingWriter _settingManager = Substitute.For<ISettingWriter>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _globalReadClient;
    private readonly HttpClient _tenantReadClient;
    private readonly HttpClient _userClient;

    public EncryptedSettingMaskingTests()
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
            .AddPolicy(SettingsPermissions.Tenant.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, SettingsPermissions.Tenant.Read));

        builder.Services.AddSingleton(_settingProvider);
        builder.Services.AddSingleton(_settingManager);
        builder.Services.AddSingleton(_currentUserService);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton<ISettingDefinitionProvider>(new EncryptedSettingProvider());
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionRegistry(sp.GetServices<ISettingDefinitionProvider>()));

        _app = builder.Build();
        _app.MapGranitGlobalSettings();
        _app.MapGranitTenantSettings();
        _app.MapGranitUserSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _globalReadClient = BuildClient(SettingsPermissions.Global.Read);
        _tenantReadClient = BuildClient(SettingsPermissions.Tenant.Read);
        _userClient = BuildClient(AuthenticatedMarker);
    }

    public async ValueTask DisposeAsync()
    {
        _globalReadClient.Dispose();
        _tenantReadClient.Dispose();
        _userClient.Dispose();
        await _app.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // GET /settings/global — encrypted values masked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllGlobal_EncryptedSetting_IsMasked()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(EncryptedSettingName, "G", null, "super-secret-key"),
                new SettingValue(PlainSettingName, "G", null, "visible-value"),
            ]);

        HttpResponseMessage response = await _globalReadClient.GetAsync(
            "/settings/global", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result[EncryptedSettingName].ShouldBe(MaskedValue);
        result[PlainSettingName].ShouldBe("visible-value");
    }

    // -------------------------------------------------------------------------
    // GET /settings/tenant — encrypted values masked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllTenant_EncryptedSetting_IsMasked()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(EncryptedSettingName, "T", TenantId.ToString(), "tenant-secret"),
                new SettingValue(PlainSettingName, "T", TenantId.ToString(), "tenant-plain"),
            ]);

        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/tenant", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result[EncryptedSettingName].ShouldBe(MaskedValue);
        result[PlainSettingName].ShouldBe("tenant-plain");
    }

    // -------------------------------------------------------------------------
    // GET /settings/user — encrypted values masked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllUser_EncryptedSetting_IsMasked()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(EncryptedSettingName, "U", TestAuthHandler.TestUserId, "user-secret"),
                new SettingValue(PlainSettingName, "U", TestAuthHandler.TestUserId, "user-plain"),
            ]);

        HttpResponseMessage response = await _userClient.GetAsync(
            "/settings/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result[EncryptedSettingName].ShouldBe(MaskedValue);
        result[PlainSettingName].ShouldBe("user-plain");
    }

    // -------------------------------------------------------------------------
    // GET /settings/user/{name} — encrypted single value masked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSingleUser_EncryptedSetting_IsMasked()
    {
        _settingProvider.GetOrNullAsync(EncryptedSettingName, Arg.Any<CancellationToken>())
            .Returns("super-secret-key");

        HttpResponseMessage response = await _userClient.GetAsync(
            $"/settings/user/{EncryptedSettingName}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SettingValueResponse? result = await response.Content
            .ReadFromJsonAsync<SettingValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe(EncryptedSettingName);
        result.Value.ShouldBe(MaskedValue);
    }

    [Fact]
    public async Task GetSingleUser_NonEncryptedSetting_ReturnsPlaintext()
    {
        _settingProvider.GetOrNullAsync(PlainSettingName, Arg.Any<CancellationToken>())
            .Returns("plain-value");

        HttpResponseMessage response = await _userClient.GetAsync(
            $"/settings/user/{PlainSettingName}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SettingValueResponse? result = await response.Content
            .ReadFromJsonAsync<SettingValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe(PlainSettingName);
        result.Value.ShouldBe("plain-value");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private HttpClient BuildClient(params string[] permissions)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }

    /// <summary>
    /// Defines one encrypted setting and one non-encrypted setting,
    /// both visible to clients, for testing masking behavior.
    /// </summary>
    private sealed class EncryptedSettingProvider : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(new SettingDefinition(EncryptedSettingName)
            {
                IsVisibleToClients = true,
                IsEncrypted = true,
                DisplayName = "Encrypted API key",
                Description = "A test encrypted setting.",
                Providers = { "U", "T", "G" },
            });

            context.Add(new SettingDefinition(PlainSettingName)
            {
                IsVisibleToClients = true,
                IsEncrypted = false,
                DisplayName = "Plain setting",
                Description = "A test non-encrypted setting.",
                Providers = { "U", "T", "G" },
            });
        }
    }
}
