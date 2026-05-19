using System.Net;
using System.Net.Http.Json;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Extensions;
using Granit.Settings.Services;
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
/// Additional tests for user settings endpoints covering edge cases:
/// hidden settings and no visible settings.
/// </summary>
public sealed class UserSettingsEndpointAdditionalTests : IAsyncDisposable
{
    private const string AuthRole = "authenticated";
    private const string Prefix = "/settings/user";

    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly ISettingManager _settingManager = Substitute.For<ISettingManager>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public UserSettingsEndpointAdditionalTests()
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

        // Register a setting that is NOT visible to clients
        builder.Services.AddSingleton<ISettingDefinitionProvider>(new HiddenSettingProvider());
        builder.Services.AddSingleton(sp =>
            new SettingDefinitionManager(sp.GetServices<ISettingDefinitionProvider>()));

        _app = builder.Build();
        _app.MapGranitUserSettings();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(AuthRole);
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetSingle_NotVisibleToClients_Returns_404()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/Hidden.Setting",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_NoVisibleSettings_Returns_EmptyDictionary()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string?>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_NotVisibleSetting_Returns_404()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/Hidden.Setting",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    /// <summary>Defines a setting NOT visible to clients.</summary>
    private sealed class HiddenSettingProvider : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(new SettingDefinition("Hidden.Setting")
            {
                IsVisibleToClients = false,
                DefaultValue = "secret",
            });
        }
    }
}
