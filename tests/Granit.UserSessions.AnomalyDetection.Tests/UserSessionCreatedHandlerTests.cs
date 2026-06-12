using Granit.IpGeolocation;
using Granit.MultiTenancy;
using Granit.UserSessions.AnomalyDetection.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.UserSessions.AnomalyDetection.Tests;

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
            .Returns(new List<UserSessionDescriptor>
            {
                new("s-new", "user-1", IsCurrent: true, Now, null, "ua", "1.1.1.1", Location: null),
                new("s-old", "user-1", IsCurrent: false, Now.AddDays(-1), Now.AddDays(-1), "ua2", "2.2.2.2", Location: null),
            });

        UserSessionCreatedEto evt = new("user-1", "s-new", TenantId: null, UserSessionSource.Bff, "ua", "1.1.1.1", Now);

        await UserSessionCreatedHandler.HandleAsync(evt, tenant, evaluator, provider, geo, Ct);

        await evaluator.Received(1).EvaluateAsync(
            Arg.Is<UserSessionDescriptor>(c =>
                c.SessionId == "s-new" && c.IsCurrent && c.Location!.City == "Brussels"),
            Arg.Is<IReadOnlyList<UserSessionDescriptor>>(h =>
                h.Count == 1 && h[0].SessionId == "s-old" && h[0].Location!.City == "Paris"),
            Arg.Any<CancellationToken>());
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
        provider.ListAsync("user-1", "s1", Arg.Any<CancellationToken>()).Returns([]);

        UserSessionCreatedEto evt = new("user-1", "s1", eventTenant, UserSessionSource.Bff, "ua", null, Now);

        await UserSessionCreatedHandler.HandleAsync(evt, tenant, evaluator, provider, geo, Ct);

        tenant.Received(1).Change(eventTenant);
    }
}
