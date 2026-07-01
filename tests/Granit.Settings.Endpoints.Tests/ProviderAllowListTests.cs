using System.Net;
using System.Net.Http.Json;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Extensions;
using Granit.Settings.Services;
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
/// Tests that settings with provider allow-lists reject writes from unauthorized scopes.
/// </summary>
public sealed class ProviderAllowListTests : IAsyncDisposable
{
    /// <summary>Setting that allows only Global scope — no User writes.</summary>
    private const string GlobalOnlySetting = "Test.GlobalOnly";

    // User-scoped endpoints use bare RequireAuthorization() — no permission gates them.
    // The caller only needs to be authenticated; this non-empty marker makes the
    // permissions header present (an empty header is dropped in transit) without
    // granting any Settings permission.
    private const string AuthenticatedMarker = "authenticated";

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingWriter _settingManager = Substitute.For<ISettingWriter>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public ProviderAllowListTests()
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
        builder.Services.AddSingleton<ISettingDefinitionProvider>(new RestrictedDefinitionProvider());
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionRegistry(sp.GetServices<ISettingDefinitionProvider>()));

        _app = builder.Build();
        _app.MapGranitUserSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Put_Setting_With_User_Not_In_AllowList_Returns_400()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"/settings/user/{GlobalOnlySetting}",
            new UpdateSettingValueRequest("value"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await _settingManager.DidNotReceive().SetForUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_Setting_With_User_Not_In_AllowList_Still_Returns_Value()
    {
        _settingProvider.GetOrNullAsync(GlobalOnlySetting, Arg.Any<CancellationToken>())
            .Returns("global-value");

        HttpResponseMessage response = await _authClient.GetAsync(
            $"/settings/user/{GlobalOnlySetting}",
            TestContext.Current.CancellationToken);

        // GET reads the resolved value (cascade) — allow-list only restricts writes.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SettingValueResponse? result = await response.Content
            .ReadFromJsonAsync<SettingValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Value.ShouldBe("global-value");
    }

    private HttpClient BuildClient()
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, AuthenticatedMarker);
        return client;
    }

    /// <summary>Defines a setting visible to clients but only writable at Global scope.</summary>
    private sealed class RestrictedDefinitionProvider : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(new SettingDefinition(GlobalOnlySetting)
            {
                IsVisibleToClients = true,
                Providers = { "G" },
            });
        }
    }
}
