using System.Net;
using System.Net.Http.Json;
using Granit.IpGeolocation;
using Granit.Testing.Endpoints;
using Granit.Users;
using Granit.UserSessions.Endpoints.Dtos;
using Granit.UserSessions.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Endpoints.Tests;

public sealed class UserSessionEndpointsHttpTests : IAsyncDisposable
{
    private readonly IUserSessionManager _manager = Substitute.For<IUserSessionManager>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GranitEndpointTestHost _host;
    private readonly HttpClient _auth;
    private readonly HttpClient _anon;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UserSessionEndpointsHttpTests()
    {
        _currentUser.UserId.Returns("user-1");

        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton(_manager);
                services.AddSingleton(_currentUser);
            },
            configureEndpoints: app => app.MapGranitUserSessions())
            .GetAwaiter().GetResult();

        _auth = _host.CreateAuthenticatedClient();
        _anon = _host.CreateAnonymousClient();
    }

    [Fact]
    public async Task ListSessions_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _anon.GetAsync("/sessions", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListSessions_Authenticated_ReturnsMappedSessions()
    {
        UserSessionDescriptor descriptor = new(
            "s1", "user-1", IsCurrent: true, CreatedAt: DateTimeOffset.UnixEpoch,
            LastAccessedAt: null, UserAgent: "curl", IpAddress: "8.8.8.8",
            Location: new GeoLocation { CountryCode = "US" });
        UserSessionRiskVerdict verdict = new(UserSessionRiskLevel.High, ["impossible_travel"], DateTimeOffset.UnixEpoch);
        _manager.ListAsync("user-1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([new UserSessionView(descriptor, verdict)]);

        UserSessionResponse[]? body = await _auth.GetFromJsonAsync<UserSessionResponse[]>("/sessions", Ct);

        body.ShouldNotBeNull();
        body.Length.ShouldBe(1);
        body[0].SessionId.ShouldBe("s1");
        body[0].IsCurrent.ShouldBeTrue();
        body[0].Location!.CountryCode.ShouldBe("US");
        body[0].RiskLevel.ShouldBe(UserSessionRiskLevel.High);
        body[0].RiskReasons.ShouldBe(["impossible_travel"]);
    }

    [Fact]
    public async Task RevokeSession_Found_ReturnsNoContent()
    {
        _manager.RevokeAsync("user-1", "s1", Arg.Any<CancellationToken>()).Returns(true);

        HttpResponseMessage response = await _auth.DeleteAsync("/sessions/s1", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeSession_NotFound_Returns404()
    {
        _manager.RevokeAsync("user-1", "missing", Arg.Any<CancellationToken>()).Returns(false);

        HttpResponseMessage response = await _auth.DeleteAsync("/sessions/missing", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeOthers_WithoutCurrentSession_Returns400()
    {
        // The test principal carries no 'sid' claim, so the current session cannot be identified.
        HttpResponseMessage response = await _auth.DeleteAsync("/sessions", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListDevices_Authenticated_ReturnsMappedDevices()
    {
        _manager.ListDevicesAsync("user-1", Arg.Any<CancellationToken>())
            .Returns([new UserDevice("d1", DeviceKind.MobileApp, "MyApp on iPhone", "iOS", null, null, 1, null)]);

        UserDeviceResponse[]? body = await _auth.GetFromJsonAsync<UserDeviceResponse[]>("/devices", Ct);

        body.ShouldNotBeNull();
        body.Length.ShouldBe(1);
        body[0].DeviceId.ShouldBe("d1");
        body[0].Kind.ShouldBe(DeviceKind.MobileApp);
        body[0].DisplayName.ShouldBe("MyApp on iPhone");
        body[0].Browser.ShouldBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        _auth.Dispose();
        _anon.Dispose();
        await _host.DisposeAsync();
    }
}
