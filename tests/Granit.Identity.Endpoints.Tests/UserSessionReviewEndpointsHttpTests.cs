using System.Net;
using System.Net.Http.Json;
using Granit.Events;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Options;
using Granit.MultiTenancy;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class UserSessionReviewEndpointsHttpTests : IAsyncDisposable
{
    private readonly ISessionReviewTokenService _tokens = Substitute.For<ISessionReviewTokenService>();
    private readonly IIdentitySecurityStateStore _securityState = Substitute.For<IIdentitySecurityStateStore>();
    private readonly IUserSessionManager _manager = Substitute.For<IUserSessionManager>();
    private readonly IDistributedEventBus _bus = Substitute.For<IDistributedEventBus>();
    private readonly GranitEndpointTestHost _host;
    private readonly HttpClient _anon;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UserSessionReviewEndpointsHttpTests()
    {
        _tokens.Validate("good").Returns(new SessionReviewTokenPayload("user-1", "s1", "dev-1", "US"));
        _tokens.Validate("bad").Returns((SessionReviewTokenPayload?)null);

        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton(_tokens);
                services.AddSingleton(_securityState);
                services.AddSingleton(_manager);
                services.AddSingleton(_bus);
                services.AddSingleton(Substitute.For<ICurrentTenant>());
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new DeviceTrustOptions()));
            },
            configureEndpoints: app => app.MapGranitUserSessions())
            .GetAwaiter().GetResult();

        _anon = _host.CreateAnonymousClient();
    }

    [Fact]
    public async Task Get_ValidToken_ReturnsContext_WithoutSideEffects()
    {
        _securityState.GetSessionReviewDecisionAsync("user-1", "s1", Arg.Any<CancellationToken>())
            .Returns((UserSessionReviewDecision?)null);

        SessionReviewContextResponse? body = await _anon
            .GetFromJsonAsync<SessionReviewContextResponse>("/sessions/review?token=good", Ct);

        body.ShouldNotBeNull();
        body.Country.ShouldBe("US");
        body.Decision.ShouldBeNull();

        // GET is side-effect-free: a link scanner prefetching it must not record, revoke, or publish anything.
        await _securityState.DidNotReceiveWithAnyArgs().TryRecordSessionReviewDecisionAsync(default!, default!, default, default, Ct);
        await _manager.DidNotReceiveWithAnyArgs().RevokeAllAsync(default!, Ct);
    }

    [Fact]
    public async Task Get_InvalidToken_Returns400()
    {
        HttpResponseMessage response = await _anon.GetAsync("/sessions/review?token=bad", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ConfirmedFirstTime_TrustsDeviceAndReinforcesProfile()
    {
        _securityState.TryRecordSessionReviewDecisionAsync("user-1", "s1", UserSessionReviewDecision.Confirmed, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _anon.PostAsJsonAsync(
            "/sessions/review",
            new SessionReviewDecisionRequest("good", UserSessionReviewDecision.Confirmed),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionReviewResultResponse? body = await response.Content.ReadFromJsonAsync<SessionReviewResultResponse>(Ct);
        body!.Applied.ShouldBeTrue();

        await _securityState.Received(1).SetDeviceTrustAsync(
            "user-1", "dev-1",
            Arg.Is<DeviceTrustVerdict>(v => v.Level == DeviceTrustLevel.Remembered && v.Reason == "user_confirmed"),
            Arg.Any<CancellationToken>());
        await _securityState.Received(1).RecordBehavioralObservationAsync(
            "user-1", "US", null, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _manager.DidNotReceiveWithAnyArgs().RevokeAllAsync(default!, Ct);
    }

    [Fact]
    public async Task Post_DeniedFirstTime_RevokesAllAndPublishesDeniedEto()
    {
        _securityState.TryRecordSessionReviewDecisionAsync("user-1", "s1", UserSessionReviewDecision.Denied, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _anon.PostAsJsonAsync(
            "/sessions/review",
            new SessionReviewDecisionRequest("good", UserSessionReviewDecision.Denied),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SessionReviewResultResponse>(Ct))!.Applied.ShouldBeTrue();

        await _manager.Received(1).RevokeAllAsync("user-1", Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(
            Arg.Is<SessionDeniedEto>(e => e.UserId == "user-1" && e.SessionId == "s1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_AlreadyReviewed_IsIdempotentNoOp()
    {
        // The single-use gate denies the second commit: no re-revoke, no re-publish.
        _securityState.TryRecordSessionReviewDecisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserSessionReviewDecision>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _securityState.GetSessionReviewDecisionAsync("user-1", "s1", Arg.Any<CancellationToken>())
            .Returns(UserSessionReviewDecision.Denied);

        HttpResponseMessage response = await _anon.PostAsJsonAsync(
            "/sessions/review",
            new SessionReviewDecisionRequest("good", UserSessionReviewDecision.Denied),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionReviewResultResponse? body = await response.Content.ReadFromJsonAsync<SessionReviewResultResponse>(Ct);
        body!.Applied.ShouldBeFalse();
        body.Decision.ShouldBe(UserSessionReviewDecision.Denied);

        await _manager.DidNotReceiveWithAnyArgs().RevokeAllAsync(default!, Ct);
        await _bus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<SessionDeniedEto>(), Ct);
    }

    [Fact]
    public async Task Post_InvalidToken_Returns400()
    {
        HttpResponseMessage response = await _anon.PostAsJsonAsync(
            "/sessions/review",
            new SessionReviewDecisionRequest("bad", UserSessionReviewDecision.Confirmed),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    public async ValueTask DisposeAsync()
    {
        _anon.Dispose();
        await _host.DisposeAsync();
    }
}
