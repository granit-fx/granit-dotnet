using Granit.Identity.AnomalyDetection.Handlers;
using Granit.IpGeolocation;
using Granit.MultiTenancy;
using NSubstitute;
using Xunit;

namespace Granit.Identity.AnomalyDetection.Tests;

public sealed class UserSessionCreatedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_ResolvesGeo_ExcludesCandidateFromHistory_AndEvaluates()
    {
        IUserSessionRiskEvaluator evaluator = Substitute.For<IUserSessionRiskEvaluator>();
        IUserSessionProvider provider = Substitute.For<IUserSessionProvider>();
        IIpGeolocationResolver geo = Substitute.For<IIpGeolocationResolver>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        tenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

        geo.ResolveAsync("1.1.1.1", Arg.Any<CancellationToken>())
            .Returns(new GeoLocation { City = "Brussels", CountryCode = "BE" });
        geo.ResolveAsync("2.2.2.2", Arg.Any<CancellationToken>())
            .Returns(new GeoLocation { City = "Paris", CountryCode = "FR" });

        // The provider returns the user's sessions (candidate + one prior), locations unresolved.
        provider.ListAsync("user-1", "s-new", Arg.Any<CancellationToken>())
            .Returns(
            [
                new("s-new", "user-1", IsCurrent: true, Now, null, "ua", "1.1.1.1", Location: null),
                new("s-old", "user-1", IsCurrent: false, Now.AddDays(-1), Now.AddDays(-1), "ua2", "2.2.2.2", Location: null),
            ]);

        IUserBehavioralProfileStore profileStore = Substitute.For<IUserBehavioralProfileStore>();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        UserSessionCreatedEto evt = new("user-1", "s-new", TenantId: null, UserSessionSource.Bff, "ua", "1.1.1.1", Now);

        await UserSessionCreatedHandler.HandleAsync(evt, tenant, evaluator, provider, geo, profileStore, timeProvider, Ct);

        await evaluator.Received(1).EvaluateAsync(
            Arg.Is<UserSessionDescriptor>(c =>
                c.SessionId == "s-new" && c.IsCurrent && c.Location!.City == "Brussels"),
            Arg.Is<IReadOnlyList<UserSessionDescriptor>>(h =>
                h.Count == 1 && h[0].SessionId == "s-old" && h[0].Location!.City == "Paris"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        // The candidate's observation is recorded into the durable profile after assessment.
        await profileStore.Received(1).RecordObservationAsync(
            "user-1", "BE", Arg.Any<string?>(), Arg.Any<string?>(), Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EstablishesTenantScopeFromEvent_WhenNoAmbientTenant()
    {
        Guid eventTenant = new("11111111-1111-1111-1111-111111111111");
        IUserSessionRiskEvaluator evaluator = Substitute.For<IUserSessionRiskEvaluator>();
        IUserSessionProvider provider = Substitute.For<IUserSessionProvider>();
        IIpGeolocationResolver geo = Substitute.For<IIpGeolocationResolver>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        tenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());
        provider.ListAsync("user-1", "s1", Arg.Any<CancellationToken>())
            .Returns(
            [
                new("s1", "user-1", IsCurrent: true, Now, null, "ua", null, Location: null),
            ]);

        IUserBehavioralProfileStore profileStore = Substitute.For<IUserBehavioralProfileStore>();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        UserSessionCreatedEto evt = new("user-1", "s1", eventTenant, UserSessionSource.Bff, "ua", null, Now);

        await UserSessionCreatedHandler.HandleAsync(evt, tenant, evaluator, provider, geo, profileStore, timeProvider, Ct);

        tenant.Received(1).Change(eventTenant);
    }

    [Fact]
    public async Task HandleAsync_DoesNotEvaluate_WhenProviderDoesNotSurfaceTheSession()
    {
        IUserSessionRiskEvaluator evaluator = Substitute.For<IUserSessionRiskEvaluator>();
        IUserSessionProvider provider = Substitute.For<IUserSessionProvider>();
        IIpGeolocationResolver geo = Substitute.For<IIpGeolocationResolver>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        tenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

        // The active provider surfaces only an unrelated session — the candidate belongs to another topology
        // layer (e.g. an OpenIddict refresh-token event reaching a BFF deployment), so it must be ignored.
        provider.ListAsync("user-1", "oidc-token-id", Arg.Any<CancellationToken>())
            .Returns(
            [
                new("bff-sid", "user-1", IsCurrent: true, Now, null, "ua", "1.1.1.1", Location: null),
            ]);

        IUserBehavioralProfileStore profileStore = Substitute.For<IUserBehavioralProfileStore>();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        UserSessionCreatedEto evt = new(
            "user-1", "oidc-token-id", TenantId: null, UserSessionSource.OpenIddict, "ua", "1.1.1.1", Now);

        await UserSessionCreatedHandler.HandleAsync(evt, tenant, evaluator, provider, geo, profileStore, timeProvider, Ct);

        await evaluator.DidNotReceiveWithAnyArgs().EvaluateAsync(default!, default!, default, Ct);
        await geo.DidNotReceiveWithAnyArgs().ResolveAsync(default, Ct);

        // A session this topology does not own is neither assessed nor recorded into the profile.
        await profileStore.DidNotReceiveWithAnyArgs().RecordObservationAsync(
            default!, default, default, default, default, Ct);
    }
}
