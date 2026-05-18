using System.Net;
using System.Net.Http.Json;
using Granit.Diagnostics.Abstractions;
using Granit.Diagnostics.Dtos;
using Granit.Diagnostics.Endpoints.Extensions;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class MonitoringEndpointsTests
{
    private const string Prefix = "/diagnostics";

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

    private static async Task<GranitEndpointTestHost> StartHostAsync(
        IHealthCheckAggregator aggregator,
        string? customPrefix = null)
    {
        return await GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy("Diagnostics.Monitoring.Read", p => p.RequireAuthenticatedUser());
                services.AddSingleton(aggregator);
            },
            configureEndpoints: app =>
            {
                if (customPrefix is null)
                {
                    app.MapGranitDiagnosticsMonitoring();
                }
                else
                {
                    app.MapGranitDiagnosticsMonitoring(opts => opts.RoutePrefix = customPrefix);
                }
            });
    }

    private static IHealthCheckAggregator BuildAggregator()
    {
        IHealthCheckAggregator agg = Substitute.For<IHealthCheckAggregator>();
        agg.CheckAllAsync(Arg.Any<CancellationToken>()).Returns(TestResponse);
        return agg;
    }

    // =========================================================================
    // GET /diagnostics/health
    // =========================================================================

    [Fact]
    public async Task GetHealth_Authenticated_Returns_Ok_With_Services()
    {
        await using GranitEndpointTestHost host = await StartHostAsync(BuildAggregator());
        using HttpClient client = host.CreateAuthenticatedClient("authenticated");

        HttpResponseMessage response = await client.GetAsync(
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
        await using GranitEndpointTestHost host = await StartHostAsync(BuildAggregator());
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHealth_Returns_Correct_Service_Statuses()
    {
        await using GranitEndpointTestHost host = await StartHostAsync(BuildAggregator());
        using HttpClient client = host.CreateAuthenticatedClient("authenticated");

        HttpResponseMessage response = await client.GetAsync(
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
        await using GranitEndpointTestHost host = await StartHostAsync(BuildAggregator());
        using HttpClient client = host.CreateAuthenticatedClient("authenticated");

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/health", TestContext.Current.CancellationToken);

        MonitoringHealthResponse? result = await response.Content
            .ReadFromJsonAsync<MonitoringHealthResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.CheckedAt.ShouldBe(new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetHealth_Returns_Tags()
    {
        await using GranitEndpointTestHost host = await StartHostAsync(BuildAggregator());
        using HttpClient client = host.CreateAuthenticatedClient("authenticated");

        HttpResponseMessage response = await client.GetAsync(
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
        await using GranitEndpointTestHost host = await StartHostAsync(
            BuildAggregator(),
            customPrefix: "admin/monitoring");
        using HttpClient client = host.CreateAuthenticatedClient("authenticated");

        HttpResponseMessage response = await client.GetAsync(
            "/admin/monitoring/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
