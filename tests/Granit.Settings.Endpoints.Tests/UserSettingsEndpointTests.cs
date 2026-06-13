using System.Net;
using System.Net.Http.Json;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Extensions;
using Granit.Settings.Endpoints.Internal;
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

public sealed class UserSettingsEndpointTests : IAsyncDisposable
{
    private const string Prefix = "/settings/user";

    // User-scoped endpoints use bare RequireAuthorization() — no permission gates them.
    // The caller only needs to be authenticated; this non-empty marker makes the
    // permissions header present (an empty header is dropped in transit) without
    // granting any Settings permission.
    private const string AuthenticatedMarker = "authenticated";

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingManager _settingManager = Substitute.For<ISettingManager>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public UserSettingsEndpointTests()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(TestAuthHandler.TestUserId);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder();

        builder.Services.AddSingleton(_settingProvider);
        builder.Services.AddSingleton(_settingManager);
        builder.Services.AddSingleton(_currentUserService);
        builder.Services.AddSingleton<ISettingDefinitionProvider, WellKnownSettingDefinitionProvider>();
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionManager(sp.GetServices<ISettingDefinitionProvider>()));

        _app = builder.Build();
        _app.MapGranitUserSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient();
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // GET /settings/user
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Authenticated_Returns_VisibleSettings()
    {
        _settingProvider.GetAllAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns([
                new SettingValue(WellKnownSettingNames.PreferredCulture, "U", TestAuthHandler.TestUserId, "fr"),
                new SettingValue(WellKnownSettingNames.PreferredTimezone, "U", TestAuthHandler.TestUserId, "Europe/Brussels"),
            ]);

        HttpResponseMessage response = await _authClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldContainKey(WellKnownSettingNames.PreferredCulture);
        result[WellKnownSettingNames.PreferredCulture].ShouldBe("fr");
        result.ShouldContainKey(WellKnownSettingNames.PreferredTimezone);
        result[WellKnownSettingNames.PreferredTimezone].ShouldBe("Europe/Brussels");
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // GET /settings/user/{name}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSingle_Known_Setting_Returns_Value()
    {
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("fr");

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SettingValueResponse? result = await response.Content
            .ReadFromJsonAsync<SettingValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe(WellKnownSettingNames.PreferredCulture);
        result.Value.ShouldBe("fr");
    }

    [Fact]
    public async Task GetSingle_Unknown_Setting_Returns_404()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Unknown.Setting",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSingle_Null_Value_Returns_200_With_Null()
    {
        _settingProvider.GetOrNullAsync(WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SettingValueResponse? result = await response.Content
            .ReadFromJsonAsync<SettingValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Value.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // PUT /settings/user/{name}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Put_Known_Setting_Returns_204_And_Calls_Manager()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("fr"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _settingManager.Received(1).SetForUserAsync(
            TestAuthHandler.TestUserId,
            WellKnownSettingNames.PreferredCulture,
            "fr",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_Null_Value_Clears_Setting()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest(null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _settingManager.Received(1).SetForUserAsync(
            TestAuthHandler.TestUserId,
            WellKnownSettingNames.PreferredCulture,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_Unknown_Setting_Returns_404()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/Unknown.Setting",
            new UpdateSettingValueRequest("value"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            new UpdateSettingValueRequest("fr"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // DELETE /settings/user/{name}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Known_Setting_Returns_204_And_Calls_Manager()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _settingManager.Received(1).DeleteAsync(
            WellKnownSettingNames.PreferredCulture,
            "U",
            TestAuthHandler.TestUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Unknown_Setting_Returns_404()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/Unknown.Setting",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/{WellKnownSettingNames.PreferredCulture}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // User-scoped endpoints gate on authentication only (RequireAuthorization with no
    // permission). Sending the permissions header authenticates the caller; the value
    // is irrelevant to authorization, so any non-empty marker satisfies the endpoint.
    private HttpClient BuildClient()
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, AuthenticatedMarker);
        return client;
    }
}
