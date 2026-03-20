using System.Net;
using System.Net.Http.Json;
using Granit.Diagnostics.Abstractions;
using Granit.Diagnostics.Dtos;
using Granit.Diagnostics.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class MonitoringEndpointsTests : IAsyncDisposable
{
    private const string AuthRole = "authenticated";
    private const string Prefix = "/diagnostics";

    private readonly IHealthCheckAggregator _aggregator = Substitute.For<IHealthCheckAggregator>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    private static readonly MonitoringHealthResponse TestResponse = new(
        [
            new ServiceHealthResponse(
                Id: "postgresql",
                Name: "Postgresql",
                Status: "healthy",
                ResponseTimeMs: 5.2,
                Description: "Primary database",
                Tags: ["readiness"]),
            new ServiceHealthResponse(
                Id: "keycloak",
                Name: "Keycloak",
                Status: "degraded",
                ResponseTimeMs: 150.0,
                Description: null,
                Tags: ["readiness", "startup"]),
        ],
        CheckedAt: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

    public MonitoringEndpointsTests()
    {
        _aggregator.CheckAllAsync(Arg.Any<CancellationToken>())
            .Returns(TestResponse);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Diagnostics.Monitoring.Read", p => p.RequireAuthenticatedUser());

        builder.Services.AddSingleton(_aggregator);

        _app = builder.Build();
        _app.MapGranitDiagnosticsMonitoring();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(AuthRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET /diagnostics/health
    // =========================================================================

    [Fact]
    public async Task GetHealth_Authenticated_Returns_Ok_With_Services()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        MonitoringHealthResponse? result = await response.Content
            .ReadFromJsonAsync<MonitoringHealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Services.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetHealth_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHealth_Returns_Correct_Service_Statuses()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        MonitoringHealthResponse? result = await response.Content
            .ReadFromJsonAsync<MonitoringHealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Services[0].Id.ShouldBe("postgresql");
        result.Services[0].Status.ShouldBe("healthy");
        result.Services[0].ResponseTimeMs.ShouldBe(5.2);
        result.Services[0].Description.ShouldBe("Primary database");

        result.Services[1].Id.ShouldBe("keycloak");
        result.Services[1].Status.ShouldBe("degraded");
    }

    [Fact]
    public async Task GetHealth_Returns_CheckedAt_Timestamp()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        MonitoringHealthResponse? result = await response.Content
            .ReadFromJsonAsync<MonitoringHealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.CheckedAt.ShouldBe(new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetHealth_Returns_Tags()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        MonitoringHealthResponse? result = await response.Content
            .ReadFromJsonAsync<MonitoringHealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Services[0].Tags.ShouldBe(["readiness"]);
        result.Services[1].Tags.ShouldBe(["readiness", "startup"]);
    }

    [Fact]
    public async Task GetHealth_CustomPrefix_Works()
    {
        await using WebApplication customApp = BuildAppWithPrefix("admin/monitoring");
        await customApp.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = BuildClient(customApp, AuthRole);

        HttpResponseMessage response = await client.GetAsync(
            "/admin/monitoring/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static HttpClient BuildClient(WebApplication app, string role)
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private WebApplication BuildAppWithPrefix(string prefix)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Diagnostics.Monitoring.Read", p => p.RequireAuthenticatedUser());

        builder.Services.AddSingleton(_aggregator);

        WebApplication app = builder.Build();
        app.MapGranitDiagnosticsMonitoring(opts => opts.RoutePrefix = prefix);
        return app;
    }
}
