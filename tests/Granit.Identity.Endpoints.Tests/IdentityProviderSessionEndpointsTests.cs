using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for identity provider session endpoints (list sessions, devices, terminate).
/// </summary>
public sealed class IdentityProviderSessionEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/provider";

    private readonly IIdentitySessionManager _sessionManager = Substitute.For<IIdentitySessionManager>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;

    public IdentityProviderSessionEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsUserCreation.Returns(true);
        _capabilities.SupportsIndividualSessionTermination.Returns(true);
        _capabilities.SupportsNativePasswordResetEmail.Returns(true);
        _capabilities.SupportsGroupHierarchy.Returns(true);
        _capabilities.SupportsCustomAttributes.Returns(true);
        _capabilities.SupportsCredentialVerification.Returns(true);
        _capabilities.MaxCustomAttributes.Returns(50);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        _sessionManager.GetUserSessionsAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new IdentitySession("session-1", "127.0.0.1", now, now, false, ["app"])]);

        _sessionManager.GetUserDeviceActivityAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new IdentityDeviceActivity(
                "127.0.0.1", now, "Desktop", "Windows", "11", "Chrome", false, true,
                [new IdentitySession("session-1", "127.0.0.1", now, now, false, ["app"])])]);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_sessionManager);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapIdentityProviderEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /users/{userId}/sessions --

    [Fact]
    public async Task GetUserSessions_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentitySession>? sessions = await response.Content
            .ReadFromJsonAsync<List<IdentitySession>>(TestContext.Current.CancellationToken);
        sessions.ShouldNotBeNull();
        sessions.Count.ShouldBe(1);
        sessions[0].SessionId.ShouldBe("session-1");
    }

    // -- GET /users/{userId}/devices --

    [Fact]
    public async Task GetUserDevices_returns_200_with_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<IdentityDeviceActivity>? devices = await response.Content
            .ReadFromJsonAsync<List<IdentityDeviceActivity>>(TestContext.Current.CancellationToken);
        devices.ShouldNotBeNull();
        devices.Count.ShouldBe(1);
        devices[0].IpAddress.ShouldBe("127.0.0.1");
    }

    // -- DELETE /users/{userId}/sessions/{sessionId} --

    [Fact]
    public async Task TerminateSession_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/session-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _sessionManager.Received(1).TerminateSessionAsync("user-1", "session-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TerminateSession_unsupported_returns_501()
    {
        _capabilities.SupportsIndividualSessionTermination.Returns(false);

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/session-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    // -- DELETE /users/{userId}/sessions --

    [Fact]
    public async Task TerminateAllSessions_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _sessionManager.Received(1).TerminateAllSessionsAsync("user-1", Arg.Any<CancellationToken>());
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
