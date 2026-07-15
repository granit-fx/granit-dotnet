using Granit.Identity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class MemoryIdentitySecurityStateStoreProfileTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MemoryIdentitySecurityStateStore _sut = new();

    [Fact]
    public async Task RecordThenGet_ReturnsOneObservationPerNonNullDimension()
    {
        await _sut.RecordBehavioralObservationAsync("u1", "BE", "windows", "50,4", T0, Ct);

        UserBehavioralProfile profile = await _sut.GetBehavioralProfileAsync("u1", Ct);

        profile.Observations.Count.ShouldBe(3);
        profile.Observations.ShouldContain(o =>
            o.Kind == BehavioralObservationKind.Country && o.Value == "BE" && o.Count == 1
            && o.FirstSeenAt == T0 && o.LastSeenAt == T0);
    }

    [Fact]
    public async Task Record_Twice_IncrementsCountAndAdvancesLastSeenKeepingFirstSeen()
    {
        await _sut.RecordBehavioralObservationAsync("u1", "BE", null, null, T0, Ct);
        await _sut.RecordBehavioralObservationAsync("u1", "BE", null, null, T0.AddDays(1), Ct);

        BehavioralObservation be = (await _sut.GetBehavioralProfileAsync("u1", Ct)).Observations
            .Single(o => o.Kind == BehavioralObservationKind.Country && o.Value == "BE");

        be.Count.ShouldBe(2);
        be.FirstSeenAt.ShouldBe(T0);
        be.LastSeenAt.ShouldBe(T0.AddDays(1));
    }

    [Fact]
    public async Task Record_SkipsNullAndEmptyValues()
    {
        await _sut.RecordBehavioralObservationAsync("u1", "BE", null, "", T0, Ct);

        (await _sut.GetBehavioralProfileAsync("u1", Ct)).Observations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_UnknownUser_ReturnsEmpty() =>
        (await _sut.GetBehavioralProfileAsync("nobody", Ct)).ShouldBe(UserBehavioralProfile.Empty);

    [Fact]
    public async Task Profiles_AreIsolatedPerUser()
    {
        await _sut.RecordBehavioralObservationAsync("u1", "BE", null, null, T0, Ct);

        (await _sut.GetBehavioralProfileAsync("u2", Ct)).Observations.ShouldBeEmpty();
    }
}
