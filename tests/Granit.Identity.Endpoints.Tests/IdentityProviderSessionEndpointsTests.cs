using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for the admin identity-provider session/device endpoints, served by the canonical
/// <see cref="IUserSessionManager"/> (list sessions, list devices, terminate one, terminate all).
/// </summary>
public sealed class IdentityProviderSessionEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-identity-admin";
    private const string SessionsViewerRole = "granit-identity-sessions-viewer";
    private const string SessionsManagerRole = "granit-identity-sessions-manager";
    private const string Prefix = "/identity/provider";

    private readonly IUserSessionManager _sessionManager = Substitute.For<IUserSessionManager>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _sessionsViewerClient;
    private readonly HttpClient _sessionsManagerClient;

    public IdentityProviderSessionEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsIndividualSessionTermination.Returns(true);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Descriptor carries the raw IP; the endpoint masks it by default (ExposeRawIpAddress = false).
        _sessionManager.ListAsync("user-1", null, Arg.Any<CancellationToken>())
            .Returns([new UserSessionView(
                new UserSessionDescriptor("session-1", "user-1", false, now, now, "agent", "127.0.0.1", null),
                null)]);

        _sessionManager.ListDevicesAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new UserDevice("device-1", DeviceKind.Browser, "Windows", "Chrome", now, 1, null)]);

        _sessionManager.RevokeAsync("user-1", "session-1", Arg.Any<CancellationToken>()).Returns(true);
        _sessionManager.RevokeAllAsync("user-1", Arg.Any<CancellationToken>()).Returns(1);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Admin holds every permission; the narrowly-scoped roles each hold exactly one
        // so the permission split between Sessions.Read and Sessions.Manage can be asserted.
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Users.Manage, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Roles.Read, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Roles.Manage, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Groups.Read, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Groups.Manage, policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Sessions.Read,
                policy => policy.RequireRole(AdminRole, SessionsViewerRole))
            .AddPolicy(IdentityPermissions.Sessions.Manage,
                policy => policy.RequireRole(AdminRole, SessionsManagerRole))
            .AddPolicy(IdentityPermissions.Passwords.Manage, policy => policy.RequireRole(AdminRole));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_sessionManager);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapGranitIdentityProvider();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _sessionsViewerClient = BuildClient(SessionsViewerRole);
        _sessionsManagerClient = BuildClient(SessionsManagerRole);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /users/{userId}/sessions --

    [Fact]
    public async Task GetUserSessions_returns_200_with_canonical_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<UserSessionResponse>? sessions = await response.Content
            .ReadFromJsonAsync<List<UserSessionResponse>>(TestContext.Current.CancellationToken);
        sessions.ShouldNotBeNull();
        sessions.Count.ShouldBe(1);
        sessions[0].SessionId.ShouldBe("session-1");
        // IP is masked by default (GDPR): 127.0.0.1 -> 127.0.0.0 (host octet zeroed).
        sessions[0].IpAddress.ShouldBe("127.0.0.0");
    }

    [Fact]
    public async Task GetUserSessions_with_sessions_read_only_returns_200()
    {
        HttpResponseMessage response = await _sessionsViewerClient.GetAsync(
            $"{Prefix}/users/user-1/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -- GET /users/{userId}/devices --

    [Fact]
    public async Task GetUserDevices_returns_200_with_canonical_list()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<UserDeviceResponse>? devices = await response.Content
            .ReadFromJsonAsync<List<UserDeviceResponse>>(TestContext.Current.CancellationToken);
        devices.ShouldNotBeNull();
        devices.Count.ShouldBe(1);
        devices[0].DeviceId.ShouldBe("device-1");
        devices[0].Kind.ShouldBe(DeviceKind.Browser);
    }

    [Fact]
    public async Task GetUserDevices_with_sessions_read_only_returns_200()
    {
        HttpResponseMessage response = await _sessionsViewerClient.GetAsync(
            $"{Prefix}/users/user-1/devices", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -- DELETE /users/{userId}/sessions/{sessionId} --

    [Fact]
    public async Task TerminateSession_returns_204()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/session-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _sessionManager.Received(1).RevokeAsync("user-1", "session-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TerminateSession_with_sessions_manage_returns_204()
    {
        HttpResponseMessage response = await _sessionsManagerClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/session-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TerminateSession_unknown_session_returns_404()
    {
        _sessionManager.RevokeAsync("user-1", "missing", Arg.Any<CancellationToken>()).Returns(false);

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/missing", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TerminateSession_with_sessions_read_only_returns_403()
    {
        HttpResponseMessage response = await _sessionsViewerClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions/session-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _sessionManager.DidNotReceive().RevokeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        await _sessionManager.Received(1).RevokeAllAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TerminateAllSessions_with_sessions_read_only_returns_403()
    {
        HttpResponseMessage response = await _sessionsViewerClient.DeleteAsync(
            $"{Prefix}/users/user-1/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _sessionManager.DidNotReceive().RevokeAllAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
