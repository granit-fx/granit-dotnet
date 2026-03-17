using System.Net;
using System.Net.Http.Json;
using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

/// <summary>
/// Integration tests for the GET /cookies/config endpoint.
/// Uses a TestServer + NSubstitute mocks for ICookieRegistry / IThirdPartyServiceRegistry.
/// The endpoint is anonymous — no authentication setup needed.
/// </summary>
public sealed class CookieConsentEndpointsTests : IAsyncDisposable
{
    private const string DefaultRoute = "/cookies/config";

    private readonly ICookieRegistry _cookieRegistry = Substitute.For<ICookieRegistry>();
    private readonly IThirdPartyServiceRegistry _serviceRegistry = Substitute.For<IThirdPartyServiceRegistry>();
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public CookieConsentEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(_cookieRegistry);
        builder.Services.AddSingleton(_serviceRegistry);

        _app = builder.Build();
        _app.MapGranitCookieConsent();
        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET /cookies/config ─────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_WithEmptyRegistries_Returns200WithEmptyLists()
    {
        // Arrange
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns([]);

        // Act
        HttpResponseMessage response = await _client.GetAsync(DefaultRoute, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieConsentConfigResponse? result =
            await response.Content.ReadFromJsonAsync<CookieConsentConfigResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Cookies.ShouldBeEmpty();
        result.Services.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetConfig_WithRegisteredCookiesAndServices_Returns200WithData()
    {
        // Arrange
        _cookieRegistry.GetAll().Returns<IReadOnlyList<CookieDefinition>>(
        [
            new("session_id", CookieCategory.StrictlyNecessary, 0, true, "Session tracking"),
            new("_ga", CookieCategory.Analytics, 730, false, "Google Analytics"),
        ]);
        _serviceRegistry.GetAll().Returns<IReadOnlyList<ThirdPartyServiceDefinition>>(
        [
            new("matomo", CookieCategory.Analytics, ["^_pk_"]),
        ]);

        // Act
        HttpResponseMessage response = await _client.GetAsync(DefaultRoute, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieConsentConfigResponse? result =
            await response.Content.ReadFromJsonAsync<CookieConsentConfigResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Cookies.Count.ShouldBe(2);
        result.Services.Count.ShouldBe(1);

        result.Cookies[0].Name.ShouldBe("session_id");
        result.Cookies[0].Category.ShouldBe("strictly_necessary");

        result.Services[0].Name.ShouldBe("matomo");
        result.Services[0].Category.ShouldBe("analytics");
    }

    [Fact]
    public async Task GetConfig_SetsCacheControlHeader()
    {
        // Arrange
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns([]);

        // Act
        HttpResponseMessage response = await _client.GetAsync(DefaultRoute, TestContext.Current.CancellationToken);

        // Assert
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(3600));
    }

    [Fact]
    public async Task GetConfig_IsAnonymous_Returns200WithoutAuthentication()
    {
        // Arrange — no auth headers on the client
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns([]);

        // Act
        HttpResponseMessage response = await _client.GetAsync(DefaultRoute, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Custom route prefix ─────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_WithCustomRoutePrefix_RespondsOnCustomRoute()
    {
        // Arrange — separate app with custom prefix
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_cookieRegistry);
        builder.Services.AddSingleton(_serviceRegistry);
        _cookieRegistry.GetAll().Returns([]);
        _serviceRegistry.GetAll().Returns([]);

        await using WebApplication customApp = builder.Build();
        customApp.MapGranitCookieConsent(opts => opts.RoutePrefix = "api/consent");
        await customApp.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient customClient = customApp.GetTestClient();

        // Act
        HttpResponseMessage response = await customClient.GetAsync(
            "/api/consent/config", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
