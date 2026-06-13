using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Extensions;
using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Endpoints.Validators;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Granit.Testing.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class AdminSettingsBulkEndpointTests : IAsyncDisposable
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string IntSettingName = "App.MaxRetries";
    private const string BoolSettingName = "App.FeatureX";
    private const string EnumSettingName = "App.LogLevel";
    private const string EncryptedSettingName = "App.Secret";
    private const string GlobalOnlySettingName = "App.GlobalOnly";

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingManager _settingManager = Substitute.For<ISettingManager>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _globalReadClient;
    private readonly HttpClient _globalManageClient;
    private readonly HttpClient _tenantReadClient;
    private readonly HttpClient _tenantManageClient;

    public AdminSettingsBulkEndpointTests()
    {
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
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton<ISettingDefinitionProvider, TestDefinitionProvider>();
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionManager(sp.GetServices<ISettingDefinitionProvider>()));
        builder.Services.AddScoped<IValidator<BulkUpdateSettingsRequest>, BulkUpdateSettingsRequestValidator>();

        _app = builder.Build();
        _app.MapGranitGlobalSettings();
        _app.MapGranitTenantSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _globalReadClient = BuildClient(SettingsPermissions.Global.Read);
        _globalManageClient = BuildClient(SettingsPermissions.Global.Manage);
        _tenantReadClient = BuildClient(SettingsPermissions.Tenant.Read);
        _tenantManageClient = BuildClient(SettingsPermissions.Tenant.Manage);
    }

    public async ValueTask DisposeAsync()
    {
        _globalReadClient.Dispose();
        _globalManageClient.Dispose();
        _tenantReadClient.Dispose();
        _tenantManageClient.Dispose();
        await _app.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // GET /settings/global/definitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitions_Global_Returns_MetadataPlusValue()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(IntSettingName, "G", null, "5"),
                new SettingValue(EncryptedSettingName, "G", null, "super-secret"),
            ]);

        HttpResponseMessage response = await _globalReadClient.GetAsync(
            "/settings/global/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminAppSettingResponse>? payload = await response.Content.ReadFromJsonAsync<List<AdminAppSettingResponse>>(
            TestContext.Current.CancellationToken);

        payload.ShouldNotBeNull();
        payload.Count.ShouldBeGreaterThanOrEqualTo(5);

        AdminAppSettingResponse intSetting = payload.Single(s => s.Key == IntSettingName);
        intSetting.Value.ShouldBe("5");
        intSetting.ValueKind.ShouldBe(ValueKind.Int);
        intSetting.AllowedValues.ShouldBe(["1", "5", "10"]);
        intSetting.IsEncrypted.ShouldBeFalse();

        AdminAppSettingResponse encrypted = payload.Single(s => s.Key == EncryptedSettingName);
        encrypted.Value.ShouldBe("***");
        encrypted.IsEncrypted.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDefinitions_Global_WrongRole_Returns_403()
    {
        HttpResponseMessage response = await _globalManageClient.GetAsync(
            "/settings/global/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDefinitions_Tenant_NoTenantContext_Returns_400()
    {
        _currentTenant.IsAvailable.Returns(false);

        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/tenant/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDefinitions_Tenant_Returns_MetadataPlusValue()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(BoolSettingName, "T", TenantId.ToString(), "true"),
            ]);

        HttpResponseMessage response = await _tenantReadClient.GetAsync(
            "/settings/tenant/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<AdminAppSettingResponse>? payload = await response.Content.ReadFromJsonAsync<List<AdminAppSettingResponse>>(
            TestContext.Current.CancellationToken);

        payload.ShouldNotBeNull();
        AdminAppSettingResponse boolSetting = payload.Single(s => s.Key == BoolSettingName);
        boolSetting.Value.ShouldBe("true");
        boolSetting.ValueKind.ShouldBe(ValueKind.Bool);
    }

    // -------------------------------------------------------------------------
    // PUT /settings/global/bulk
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BulkUpdate_Global_AllValid_Returns_AllUpdated()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "10"),
            new(BoolSettingName, "true"),
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload.ShouldNotBeNull();
        payload.Results.Count.ShouldBe(2);
        payload.Results.ShouldAllBe(r => r.Outcome == BulkSettingOutcome.Updated);
        payload.Results.ShouldAllBe(r => r.ErrorCode == null);

        await _settingManager.Received(1).SetGlobalAsync(IntSettingName, "10", Arg.Any<CancellationToken>());
        await _settingManager.Received(1).SetGlobalAsync(BoolSettingName, "true", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Global_UnknownKey_Returns_NotFoundOutcome()
    {
        BulkUpdateSettingsRequest request = new([
            new("Unknown.Setting", "value"),
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.NotFound);
        payload.Results[0].ErrorCode.ShouldBe("Granit:Settings:NotFound");

        await _settingManager.DidNotReceive().SetGlobalAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Global_ProviderNotAllowed_When_Setting_Restricts_Scope()
    {
        // GlobalOnlySettingName is defined with Providers = { "G" }, so tenant PUT is disallowed
        // but global PUT is allowed. We flip this test to tenant to check the outcome path.
        BulkUpdateSettingsRequest request = new([
            new(GlobalOnlySettingName, "value"),
        ]);

        HttpResponseMessage response = await _tenantManageClient.PutAsJsonAsync(
            "/settings/tenant/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.ProviderNotAllowed);
        payload.Results[0].ErrorCode.ShouldBe("Granit:Settings:ProviderNotAllowed");
    }

    [Fact]
    public async Task BulkUpdate_Global_InvalidValueKind_Returns_ValidationFailed()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "not-a-number"),
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.ValidationFailed);
        payload.Results[0].ErrorCode.ShouldBe("Granit:Settings:ValidationFailed");

        await _settingManager.DidNotReceive().SetGlobalAsync(
            IntSettingName, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Global_ValueOutsideAllowedValues_Returns_ValidationFailed()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "99"), // parses as int but not in [1, 5, 10]
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.ValidationFailed);
    }

    [Fact]
    public async Task BulkUpdate_Global_NullValue_Is_Treated_As_Clear_And_Succeeds()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, null),
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.Updated);

        await _settingManager.Received(1).SetGlobalAsync(
            IntSettingName, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Global_MixedOutcomes_AppliesEachIndependently()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "5"),            // Updated
            new("Unknown.Key", "x"),             // NotFound
            new(BoolSettingName, "banana"),      // ValidationFailed
        ]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        BulkUpdateSettingsResponse? payload = await response.Content.ReadFromJsonAsync<BulkUpdateSettingsResponse>(
            TestContext.Current.CancellationToken);

        payload!.Results[0].Outcome.ShouldBe(BulkSettingOutcome.Updated);
        payload.Results[1].Outcome.ShouldBe(BulkSettingOutcome.NotFound);
        payload.Results[2].Outcome.ShouldBe(BulkSettingOutcome.ValidationFailed);

        await _settingManager.Received(1).SetGlobalAsync(
            IntSettingName, "5", Arg.Any<CancellationToken>());
        await _settingManager.DidNotReceive().SetGlobalAsync(
            BoolSettingName, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Global_EmptySettings_Returns_422()
    {
        BulkUpdateSettingsRequest request = new([]);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BulkUpdate_Global_TooManyEntries_Returns_422()
    {
        BulkSettingEntry[] entries = Enumerable.Range(0, BulkUpdateSettingsRequestValidator.MaxEntries + 1)
            .Select(i => new BulkSettingEntry($"key-{i}", "v"))
            .ToArray();
        BulkUpdateSettingsRequest request = new(entries);

        HttpResponseMessage response = await _globalManageClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BulkUpdate_Global_ReadOnlyRole_Returns_403()
    {
        BulkUpdateSettingsRequest request = new([new(IntSettingName, "5")]);

        HttpResponseMessage response = await _globalReadClient.PutAsJsonAsync(
            "/settings/global/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // PUT /settings/tenant/bulk
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BulkUpdate_Tenant_Authorized_AppliesToCurrentTenant()
    {
        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "5"),
        ]);

        HttpResponseMessage response = await _tenantManageClient.PutAsJsonAsync(
            "/settings/tenant/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await _settingManager.Received(1).SetForTenantAsync(
            TenantId, IntSettingName, "5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdate_Tenant_NoTenantContext_Returns_400()
    {
        _currentTenant.IsAvailable.Returns(false);
        _currentTenant.Id.Returns((Guid?)null);

        BulkUpdateSettingsRequest request = new([
            new(IntSettingName, "5"),
        ]);

        HttpResponseMessage response = await _tenantManageClient.PutAsJsonAsync(
            "/settings/tenant/bulk", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

    private sealed class TestDefinitionProvider : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(new SettingDefinition(IntSettingName)
            {
                ValueKind = ValueKind.Int,
                AllowedValues = ["1", "5", "10"],
                DefaultValue = "5",
            });

            context.Add(new SettingDefinition(BoolSettingName)
            {
                ValueKind = ValueKind.Bool,
                DefaultValue = "false",
            });

            context.Add(new SettingDefinition(EnumSettingName)
            {
                AllowedValues = ["Debug", "Information", "Warning"],
                DefaultValue = "Information",
            });

            context.Add(new SettingDefinition(EncryptedSettingName)
            {
                IsEncrypted = true,
            });

            context.Add(new SettingDefinition(GlobalOnlySettingName)
            {
                Providers = { "G" },
            });
        }
    }
}
